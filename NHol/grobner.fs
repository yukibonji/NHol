﻿(*

Copyright 1998 University of Cambridge
Copyright 1998-2007 John Harrison
Copyright 2013 Jack Pappas, Eric Taucher, Domenico Masini

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

*)

#if INTERACTIVE
#else
/// Groebner basis procedure for most semirings.
module NHol.grobner

open FSharp.Compatibility.OCaml
open FSharp.Compatibility.OCaml.Num

open NHol
open lib
open fusion
open fusion.Hol_kernel
open basics
open nets
open printer
open preterm
open parser
open equal
open bool
open drule
open tactics
open itab
open simp
open theorems
open ind_defs
open ``class``
open trivia
open canon
open meson
open quot
//open pair
open nums
open recursion
open arith
//open wf
open calc_num
open normalizer
#endif


(* ------------------------------------------------------------------------- *)
(* Type for recording history, i.e. how a polynomial was obtained.           *)
(* ------------------------------------------------------------------------- *)

type history =
 | Start of int
 | Mmul of (num * (int list)) * history
 | Add of history * history;;

(* ------------------------------------------------------------------------- *)
(* Overall function; everything else is local.                               *)
(* ------------------------------------------------------------------------- *)

/// Returns a pair giving a ring proof procedure and an ideal membership routine.
let RING_AND_IDEAL_CONV = 

  (* ----------------------------------------------------------------------- *)
  (* Monomial ordering.                                                      *)
  (* ----------------------------------------------------------------------- *)
  
  let morder_lt =
    let rec lexorder l1 l2 =
      match (l1,l2) with
        | [],[] -> false
        | (x1::o1,x2::o2) -> x1 > x2 || x1 = x2 && lexorder o1 o2
        | _ -> failwith "morder: inconsistent monomial lengths"
    fun m1 m2 -> let n1 = itlist (+) m1 0
                 let n2 = itlist (+) m2 0
                 n1 < n2 || n1 = n2 && lexorder m1 m2
  
  (* ----------------------------------------------------------------------- *)
  (* Arithmetic on canonical polynomials.                                    *)
  (* ----------------------------------------------------------------------- *)
  
  let grob_neg = map (fun (c,m) -> (minus_num c,m))
  
  let rec grob_add l1 l2 =
    match (l1,l2) with
      ([],l2) -> l2
    | (l1,[]) -> l1
    | ((c1,m1)::o1,(c2,m2)::o2) ->
          if m1 = m2 then
            let c = c1+c2 
            let rest = grob_add o1 o2 in
            if c = num_0 then rest else (c,m1)::rest
          else if morder_lt m2 m1 then (c1,m1)::(grob_add o1 l2)
          else (c2,m2)::(grob_add l1 o2)
  
  let grob_sub l1 l2 = grob_add l1 (grob_neg l2)
  
  let grob_mmul (c1,m1) (c2,m2) = (c1*c2,map2 (+) m1 m2)
  
  let rec grob_cmul cm pol = map (grob_mmul cm) pol
  
  let rec grob_mul l1 l2 =
    match l1 with
      [] -> []
    | (h1::t1) -> grob_add (grob_cmul h1 l2) (grob_mul t1 l2)
  
  let grob_inv l =
    match l with
      [c,vs] when forall (fun x -> x = 0) vs ->
          if c = num_0 then failwith "grob_inv: division by zero"
          else [num_1 / c,vs]
    | _ -> failwith "grob_inv: non-constant divisor polynomial"
  
  let grob_div l1 l2 =
    match l2 with
      [c,l] when forall (fun x -> x = 0) l ->
          if c = num_0 then failwith "grob_div: division by zero"
          else grob_cmul (num_1 / c,l) l1
    | _ -> failwith "grob_div: non-constant divisor polynomial"
  
  let rec grob_pow vars l n =
    if n < 0 then failwith "grob_pow: negative power"
    else if n = 0 then [num_1,map (fun v -> 0) vars]
    else grob_mul l (grob_pow vars l (n - 1))
  
  (* ----------------------------------------------------------------------- *)
  (* Monomial division operation.                                            *)
  (* ----------------------------------------------------------------------- *)
  
  let mdiv (c1,m1) (c2,m2) =
    (c1/c2,
     map2 (fun n1 n2 -> if n1 < n2 then failwith "mdiv" else n1-n2) m1 m2)
  
  (* ----------------------------------------------------------------------- *)
  (* Lowest common multiple of two monomials.                                *)
  (* ----------------------------------------------------------------------- *)
  
  let mlcm (c1,m1) (c2,m2) = (num_1,map2 max m1 m2)
  
  (* ----------------------------------------------------------------------- *)
  (* Reduce monomial cm by polynomial pol, returning replacement for cm.     *)
  (* ----------------------------------------------------------------------- *)
  
  let reduce1 cm (pol,hpol) =
    match pol with
      [] -> failwith "reduce1"
    | cm1::cms -> try let (c,m) = mdiv cm cm1 in
                      (grob_cmul (minus_num c,m) cms,
                       Mmul((minus_num c,m),hpol))
                  with Failure _ as e -> nestedFailwith e "reduce1"
  
  (* ----------------------------------------------------------------------- *)
  (* Try this for all polynomials in a basis.                                *)
  (* ----------------------------------------------------------------------- *)
  
  let reduceb cm basis = tryfind (fun p -> reduce1 cm p) basis
  
  (* ----------------------------------------------------------------------- *)
  (* Reduction of a polynomial (always picking largest monomial possible).   *)
  (* ----------------------------------------------------------------------- *)
  
  let rec reduce basis (pol,hist) =
    match pol with
      [] -> (pol,hist)
    | cm::ptl -> try let q,hnew = reduceb cm basis
                     reduce basis (grob_add q ptl,Add(hnew,hist))
                 with Failure _ ->
                     let q,hist' = reduce basis (ptl,hist)
                     cm::q,hist'
  
  (* ----------------------------------------------------------------------- *)
  (* Check for orthogonality w.r.t. LCM.                                     *)
  (* ----------------------------------------------------------------------- *)
  
  let orthogonal l p1 p2 =
    snd l = snd(grob_mmul (hd p1) (hd p2))
  
  (* ----------------------------------------------------------------------- *)
  (* Compute S-polynomial of two polynomials.                                *)
  (* ----------------------------------------------------------------------- *)
  
  let spoly cm ph1 ph2 =
    match (ph1,ph2) with
      ([],h),p -> ([],h)
    | p,([],h) -> ([],h)
    | (cm1::ptl1,his1),(cm2::ptl2,his2) ->
          (grob_sub (grob_cmul (mdiv cm cm1) ptl1)
                    (grob_cmul (mdiv cm cm2) ptl2),
           Add(Mmul(mdiv cm cm1,his1),
               Mmul(mdiv (minus_num(fst cm),snd cm) cm2,his2)))
  
  (* ----------------------------------------------------------------------- *)
  (* Make a polynomial monic.                                                *)
  (* ----------------------------------------------------------------------- *)
  
  let monic (pol,hist) =
    if pol = [] then (pol,hist) else
    let c',m' = hd pol in
    (map (fun (c,m) -> (c/c',m)) pol,
     Mmul((num_1 / c',map (K 0) m'),hist))
  
  (* ----------------------------------------------------------------------- *)
  (* The most popular heuristic is to order critical pairs by LCM monomial.  *)
  (* ----------------------------------------------------------------------- *)
  
  let forder ((c1,m1),_) ((c2,m2),_) = morder_lt m1 m2
  
  (* ----------------------------------------------------------------------- *)
  (* Stupid stuff forced on us by lack of equality test on num type.         *)
  (* ----------------------------------------------------------------------- *)
  
  let rec poly_lt p q =
    match (p,q) with
      p,[] -> false
    | [],q -> true
    | (c1,m1)::o1,(c2,m2)::o2 ->
          c1 < c2 ||
          c1 = c2 && (m1 < m2 || m1 = m2 && poly_lt o1 o2)
  
  let align ((p,hp),(q,hq)) =
    if poly_lt p q then ((p,hp),(q,hq)) else ((q,hq),(p,hp))
  
  let poly_eq p1 p2 =
    forall2 (fun (c1,m1) (c2,m2) -> c1 = c2 && m1 = m2) p1 p2
  
  let memx ((p1,h1),(p2,h2)) ppairs =
    not (exists (fun ((q1,_),(q2,_)) -> poly_eq p1 q1 && poly_eq p2 q2)
                ppairs)
  
  (* ----------------------------------------------------------------------- *)
  (* Buchberger's second criterion.                                          *)
  (* ----------------------------------------------------------------------- *)
  
  let criterion2 basis (lcm,((p1,h1),(p2,h2))) opairs =
    /// Tests for failure.
    let can f x = 
        try f x |> ignore; true
        with Failure _ -> false

    exists (fun g -> not(poly_eq (fst g) p1) && not(poly_eq (fst g) p2) &&
                     can (mdiv lcm) (hd(fst g)) &&
                     not(memx (align(g,(p1,h1))) (map snd opairs)) &&
                     not(memx (align(g,(p2,h2))) (map snd opairs))) basis
  
  (* ----------------------------------------------------------------------- *)
  (* Test for hitting constant polynomial.                                   *)
  (* ----------------------------------------------------------------------- *)
  
  let constant_poly p =
    length p = 1 && forall ((=) 0) (snd(hd p))
  
  (* ----------------------------------------------------------------------- *)
  (* Grobner basis algorithm.                                                *)
  (* ----------------------------------------------------------------------- *)
  
  let rec grobner_basis basis pairs =
    Format.print_string(string(length basis)+" basis elements and "+
                        string(length pairs)+" critical pairs");
    Format.print_newline();
    match pairs with
      [] -> basis
    | (l,(p1,p2))::opairs ->
          let (sp,hist as sph) = monic (reduce basis (spoly l p1 p2))
          if sp = [] || criterion2 basis (l,(p1,p2)) opairs
          then grobner_basis basis opairs else
          if constant_poly sp then grobner_basis (sph::basis) [] else
          let rawcps =
            map (fun p -> mlcm (hd(fst p)) (hd sp),align(p,sph)) basis
          let newcps = filter (fun (l,(p,q)) -> not(orthogonal l (fst p) (fst q))) rawcps
          grobner_basis (sph::basis)
                  (merge forder opairs (mergesort forder newcps))
  
  (* ----------------------------------------------------------------------- *)
  (* Interreduce initial polynomials.                                        *)
  (* ----------------------------------------------------------------------- *)
  
  let rec grobner_interreduce rpols ipols =
    match ipols with
      [] -> map monic (rev rpols)
    | p::ps -> let p' = reduce (rpols @ ps) p
               if fst p' = [] then grobner_interreduce rpols ps
               else grobner_interreduce (p'::rpols) ps
  
  (* ----------------------------------------------------------------------- *)
  (* Overall function.                                                       *)
  (* ----------------------------------------------------------------------- *)
  
  let grobner pols =
    let npols = map2 (fun p n -> p,Start n) pols (0--(length pols - 1))
    let phists = filter (fun (p,_) -> p <> []) npols
    let bas = grobner_interreduce [] (map monic phists)
    let prs0 = allpairs (fun x y -> x,y) bas bas
    let prs1 = filter (fun ((x,_),(y,_)) -> poly_lt x y) prs0
    let prs2 = map (fun (p,q) -> mlcm (hd(fst p)) (hd(fst q)),(p,q)) prs1
    let prs3 =
      filter (fun (l,(p,q)) -> not(orthogonal l (fst p) (fst q))) prs2
    grobner_basis bas (mergesort forder prs3)
  
  (* ----------------------------------------------------------------------- *)
  (* Get proof of contradiction from Grobner basis.                          *)
  (* ----------------------------------------------------------------------- *)
  
  let grobner_refute pols =
    let gb = grobner pols
    snd(find (fun (p,h) -> length p = 1 && forall ((=)0) (snd(hd p))) gb)
  
  (* ----------------------------------------------------------------------- *)
  (* Turn proof into a certificate as sum of multipliers.                    *)
  (*                                                                         *)
  (* In principle this is very inefficient: in a heavily shared proof it may *)
  (* make the same calculation many times. Could add a cache or something.   *)
  (* ----------------------------------------------------------------------- *)
  
  let rec resolve_proof vars prf =
    match prf with
      Start(-1) -> []
    | Start m -> [m,[num_1,map (K 0) vars]]
    | Mmul(pol,lin) ->
          let lis = resolve_proof vars lin
          map (fun (n,p) -> n,grob_cmul pol p) lis
    | Add(lin1,lin2) ->
          let lis1 = resolve_proof vars lin1
          let lis2 = resolve_proof vars lin2
          let dom = setify(union (map fst lis1) (map fst lis2))
          map (fun n ->
            let a = defaultArg (assoc n lis1) []
            let b = defaultArg (assoc n lis2) []
            n,grob_add a b) dom
  
  (* ----------------------------------------------------------------------- *)
  (* Run the procedure and produce Weak Nullstellensatz certificate.         *)
  (* ----------------------------------------------------------------------- *)
  
  let grobner_weak vars pols =
    let cert = resolve_proof vars (grobner_refute pols)
    let l = itlist (itlist (lcm_num << denominator << fst) << snd) cert (num_1)
    l,map (fun (i,p) -> i,map (fun (d,m) -> (l*d,m)) p) cert
  
  (* ----------------------------------------------------------------------- *)
  (* Prove polynomial is in ideal generated by others, using Grobner basis.  *)
  (* ----------------------------------------------------------------------- *)
  
  let grobner_ideal vars pols pol =
    let pol',h = reduce (grobner pols) (grob_neg pol,Start(-1))
    if pol' <> [] then failwith "grobner_ideal: not in the ideal" else
    resolve_proof vars h
  
  (* ----------------------------------------------------------------------- *)
  (* Produce Strong Nullstellensatz certificate for a power of pol.          *)
  (* ----------------------------------------------------------------------- *)
  
  let grobner_strong vars pols pol =
    if pol = [] then 1,num_1,[] else
    let vars' = (concl TRUTH)::vars
    let grob_z = [num_1,1::(map (fun x -> 0) vars)]
    let grob_1 = [num_1,(map (fun x -> 0) vars')]
    let augment = map (fun (c,m) -> (c,0::m))
    let pols' = map augment pols
    let pol' = augment pol
    let allpols = (grob_sub (grob_mul grob_z pol') grob_1)::pols'
    let l,cert = grobner_weak vars' allpols
    let d = itlist (itlist (max << hd << snd) << snd) cert 0
    let transform_monomial (c,m) =
      grob_cmul (c,tl m) (grob_pow vars pol (d - hd m))
    let transform_polynomial q = itlist (grob_add << transform_monomial) q []
    let cert' = map (fun (c,q) -> c-1,transform_polynomial q)
                    (filter (fun (k,_) -> k <> 0) cert)
    d,l,cert'
    
  (* ----------------------------------------------------------------------- *)
  (* Overall parametrized universal procedure for (semi)rings.               *)
  (* We return an IDEAL_CONV and the actual ring prover.                     *)
  (* ----------------------------------------------------------------------- *)
  
  let pth_step = 
    prove ((parse_term @"!(add:A->A->A) (mul:A->A->A) (n0:A).
          (!x. mul n0 x = n0) /\
          (!x y z. (add x y = add x z) <=> (y = z)) /\
          (!w x y z. (add (mul w y) (mul x z) = add (mul w z) (mul x y)) <=>
                     (w = x) \/ (y = z))
          ==> (!a b c d. ~(a = b) /\ ~(c = d) <=>
                         ~(add (mul a c) (mul b d) =
                           add (mul a d) (mul b c))) /\
              (!n a b c d. ~(n = n0)
                           ==> (a = b) /\ ~(c = d)
                               ==> ~(add a (mul n c) = add b (mul n d)))"),
     REPEAT GEN_TAC |>THEN<| STRIP_TAC |>THEN<|
     ASM_REWRITE_TAC[GSYM DE_MORGAN_THM] |>THEN<|
     REPEAT GEN_TAC |>THEN<| DISCH_TAC |>THEN<| STRIP_TAC |>THEN<|
     FIRST_X_ASSUM(MP_TAC << SPECL [(parse_term @"n0:A"); (parse_term @"n:A"); (parse_term @"d:A"); (parse_term @"c:A")]) |>THEN<|
     ONCE_REWRITE_TAC[GSYM CONTRAPOS_THM] |>THEN<| ASM_SIMP_TAC[])
  
  let FINAL_RULE = MATCH_MP(TAUT (parse_term @"(p ==> F) ==> (~q = p) ==> q"))
  let false_tm = (parse_term @"F") in
  let rec refute_disj rfn tm =
    match tm with
      Comb(Comb(Const("\\/",_),l),r) ->
        DISJ_CASES (ASSUME tm) (refute_disj rfn l) (refute_disj rfn r)
    | _ -> rfn tm
  
  fun (ring_dest_const,ring_mk_const,RING_EQ_CONV, ring_neg_tm,ring_add_tm,ring_sub_tm, ring_inv_tm,ring_mul_tm,ring_div_tm,ring_pow_tm, RING_INTEGRAL,RABINOWITSCH_THM,RING_NORMALIZE_CONV) ->
    let INITIAL_CONV =
      TOP_DEPTH_CONV BETA_CONV |>THENC<|
      PRESIMP_CONV |>THENC<|
      CONDS_ELIM_CONV |>THENC<|
      NNF_CONV |>THENC<|
      (if is_iff(snd(strip_forall(concl RABINOWITSCH_THM)))
       then GEN_REWRITE_CONV ONCE_DEPTH_CONV [RABINOWITSCH_THM]
       else ALL_CONV) |>THENC<|
      GEN_REWRITE_CONV REDEPTH_CONV
       [AND_FORALL_THM;
        LEFT_AND_FORALL_THM;
        RIGHT_AND_FORALL_THM;
        LEFT_OR_FORALL_THM;
        RIGHT_OR_FORALL_THM;
        OR_EXISTS_THM;
        LEFT_OR_EXISTS_THM;
        RIGHT_OR_EXISTS_THM;
        LEFT_AND_EXISTS_THM;
        RIGHT_AND_EXISTS_THM] in
    let ring_dest_neg t =
      let l,r = Choice.get <| dest_comb t in
      if l = ring_neg_tm then r else failwith "ring_dest_neg"
    let ring_dest_inv t =
      let l,r = Choice.get <| dest_comb t in
      if l = ring_inv_tm then r else failwith "ring_dest_inv"
    let ring_dest_add = dest_binop ring_add_tm
    let ring_mk_add = mk_binop ring_add_tm    
    let ring_dest_sub = dest_binop ring_sub_tm
    let ring_dest_mul = dest_binop ring_mul_tm
    let ring_mk_mul = mk_binop ring_mul_tm    
    let ring_dest_div = dest_binop ring_div_tm
    let ring_dest_pow = dest_binop ring_pow_tm
    let ring_mk_pow = mk_binop ring_pow_tm in 
    let rec grobvars tm acc =
      /// Tests for failure.
      let can f x = 
          try f x |> ignore; true
          with Failure _ -> false
      if can ring_dest_const tm then acc                            
      else if can ring_dest_neg tm then grobvars (Choice.get <| rand tm) acc      
      else if can ring_dest_pow tm && is_numeral (Choice.get <| rand tm)          
           then grobvars (lhand tm) acc
      else if can ring_dest_add tm || can ring_dest_sub tm
           || can ring_dest_mul tm
           then grobvars (lhand tm) (grobvars (Choice.get <| rand tm) acc)
      else if can ring_dest_inv tm then
           let gvs = grobvars (Choice.get <| rand tm) [] in
           if gvs = [] then acc else tm::acc
      else if can ring_dest_div tm then
           let lvs = grobvars (lhand tm) acc
           let gvs = grobvars (Choice.get <| rand tm) [] in
           if gvs = [] then lvs else tm::acc
      else tm::acc in
  
    let rec grobify_term vars tm =
      try 
        if not(mem tm vars) 
        then failwith "" 
        else [num_1,map (fun i -> if i = tm then 1 else 0) vars]
      with 
      | Failure _ -> 
      try
          let x = ring_dest_const tm in
          if x = num_0 
          then [] 
          else [x,map (fun v -> 0) vars]
      with 
      | Failure _ -> 
      try
          grob_neg(grobify_term vars (ring_dest_neg tm))
      with 
      | Failure _ -> 
      try
          grob_inv(grobify_term vars (ring_dest_inv tm))
      with 
      | Failure _ -> 
      try
          let l,r = ring_dest_add tm in
          grob_add (grobify_term vars l) (grobify_term vars r)
      with 
      | Failure _ -> 
      try
          let l,r = ring_dest_sub tm in
          grob_sub (grobify_term vars l) (grobify_term vars r)
      with 
      | Failure _ -> 
      try
          let l,r = ring_dest_mul tm in
          grob_mul (grobify_term vars l) (grobify_term vars r)
      with 
      | Failure _ -> 
      try
          let l,r = ring_dest_div tm in
          grob_div (grobify_term vars l) (grobify_term vars r)
      with 
      | Failure _ -> 
      try
          let l,r = ring_dest_pow tm in
          grob_pow vars (grobify_term vars l) (dest_small_numeral r)
      with 
      | Failure _ as e ->
            nestedFailwith e "grobify_term: unknown or invalid term" in
  
    let grobify_equation vars tm =
      let l,r = dest_eq tm in
      grob_sub (grobify_term vars l) (grobify_term vars r) in
    let grobify_equations tm =
      let cjs = conjuncts tm in
      let rawvars =
        itlist (fun eq a -> grobvars (lhand eq) (grobvars (Choice.get <| rand eq) a))
               cjs [] in
      let vars = sort (fun x y -> x < y) (setify rawvars) in
      vars,map (grobify_equation vars) cjs in
    let holify_polynomial =
      let holify_varpow (v,n) =
        if n = 1 then v else ring_mk_pow v (mk_small_numeral n) in
      let holify_monomial vars (c,m) =
        let xps = map holify_varpow (filter (fun (_,n) -> n <> 0) (zip vars m)) in
        end_itlist ring_mk_mul (ring_mk_const c :: xps) in
      let holify_polynomial vars p =
        if p = [] then ring_mk_const (num_0)
        else end_itlist ring_mk_add (map (holify_monomial vars) p) in
      holify_polynomial in
    let (pth_idom,pth_ine) = CONJ_PAIR(MATCH_MP pth_step RING_INTEGRAL) in                   
    let IDOM_RULE = CONV_RULE(REWR_CONV pth_idom) in                                         
    let PROVE_NZ n = EQF_ELIM(RING_EQ_CONV (mk_eq(ring_mk_const n,ring_mk_const(num_0)))) in 
    let NOT_EQ_01 = PROVE_NZ (num_1)                                                         
    let INE_RULE n = MATCH_MP(MATCH_MP pth_ine (PROVE_NZ n))                                 
    let MK_ADD th1 th2 = MK_COMB(AP_TERM ring_add_tm th1,th2) in                             
    let execute_proof vars eths prf =                                                        
      let x,th1 = SPEC_VAR(CONJUNCT1(CONJUNCT2 RING_INTEGRAL)) in
      let y,th2 = SPEC_VAR th1 in
      let z,th3 = SPEC_VAR th2 in
      let SUB_EQ_RULE = GEN_REWRITE_RULE I [SYM(INST [Choice.get <| mk_comb(ring_neg_tm,z),x] th3)] in
      let initpols = map (CONV_RULE(BINOP_CONV RING_NORMALIZE_CONV) << SUB_EQ_RULE) eths in
      let ADD_RULE th1 th2 =
         CONV_RULE (BINOP_CONV RING_NORMALIZE_CONV)
                   (MK_COMB(AP_TERM ring_add_tm th1,th2))
      let MUL_RULE vars m th =
         CONV_RULE (BINOP_CONV RING_NORMALIZE_CONV)
                   (AP_TERM (Choice.get <| mk_comb(ring_mul_tm,holify_polynomial vars [m]))
                            th) in
      let execache = ref [] in
      let memoize prf x = (execache := (prf,x)::(!execache)); x in
      let rec assoceq a l =
        match l with
         [] -> failwith "assoceq"
        | (x,y)::t -> if x==a then y else assoceq a t in
      let rec run_proof vars prf =
        try assoceq prf (!execache) with Failure _ ->
        (match prf with
           Start m -> el m initpols
         | Add(p1,p2) ->
            memoize prf (ADD_RULE (run_proof vars p1) (run_proof vars p2))
         | Mmul(m,p2) ->
            memoize prf (MUL_RULE vars m (run_proof vars p2))) in
      let th = run_proof vars prf in
      execache := []; CONV_RULE RING_EQ_CONV th in
  
    let REFUTE tm =
      if tm = false_tm then ASSUME tm 
      else
      let nths0,eths0 = partition (is_neg << concl) (CONJUNCTS(ASSUME tm)) in
      let nths = filter (is_eq << Choice.get << rand << concl) nths0
      let eths = filter (is_eq << concl) eths0 in
      if eths = [] then
        let th1 = end_itlist (fun th1 th2 -> IDOM_RULE(CONJ th1 th2)) nths in
        let th2 = CONV_RULE(RAND_CONV(BINOP_CONV RING_NORMALIZE_CONV)) th1 in
        let l,r = dest_eq(Choice.get <| rand(concl th2)) in
        EQ_MP (EQF_INTRO th2) (REFL l)
      else if nths = [] && not(is_var ring_neg_tm) then
        let vars,pols = grobify_equations(list_mk_conj(map concl eths)) in
        execute_proof vars eths (grobner_refute pols)
      else
      let vars,l,cert,noteqth =
        if nths = [] then
          let vars,pols = grobify_equations(list_mk_conj(map concl eths)) in
          let l,cert = grobner_weak vars pols in
          vars,l,cert,NOT_EQ_01
        else
          let nth = end_itlist (fun th1 th2 -> IDOM_RULE(CONJ th1 th2)) nths in
          match grobify_equations(list_mk_conj((Choice.get <| rand(concl nth))::map concl eths)) with
          | vars,pol::pols -> 
              let deg,l,cert = grobner_strong vars pols pol in
              let th1 = CONV_RULE(RAND_CONV(BINOP_CONV RING_NORMALIZE_CONV)) nth in
              let th2 = funpow deg (IDOM_RULE << CONJ th1) NOT_EQ_01 in
              vars,l,cert,th2
          | _ -> failwith "REFUTE: Unhandled case."
  
      Format.print_string("Translating certificate to HOL inferences");
      Format.print_newline();
      let cert_pos = map (fun (i,p) -> i,filter (fun (c,m) -> c > num_0) p) cert
      let cert_neg = map (fun (i,p) -> i,map (fun (c,m) -> minus_num c,m) (filter (fun (c,m) -> c < num_0) p)) cert in
      let herts_pos = map (fun (i,p) -> i,holify_polynomial vars p) cert_pos
      let herts_neg = map (fun (i,p) -> i,holify_polynomial vars p) cert_neg in
      let thm_fn pols =
        if pols = [] 
        then REFL(ring_mk_const num_0) 
        else end_itlist MK_ADD (map (fun (i,p) -> AP_TERM(Choice.get <| mk_comb(ring_mul_tm,p)) (el i eths)) pols) in
      let th1 = thm_fn herts_pos 
      let th2 = thm_fn herts_neg in
      let th3 = CONJ(MK_ADD (SYM th1) th2) noteqth in
      let th4 = CONV_RULE (RAND_CONV(BINOP_CONV RING_NORMALIZE_CONV)) (INE_RULE l th3) in
      let l,r = dest_eq(Choice.get <| rand(concl th4)) in
      EQ_MP (EQF_INTRO th4) (REFL l) in
  
    let RING tm =
      let avs = frees tm in
      let tm' = list_mk_forall(avs,tm) in
      let th1 = INITIAL_CONV(mk_neg tm') in
      let evs,bod = strip_exists(Choice.get <| rand(concl th1)) in
      if is_forall bod then failwith "RING: non-universal formula" else
      let th1a = WEAK_DNF_CONV bod in
      let boda = Choice.get <| rand(concl th1a) in
      let th2a = refute_disj REFUTE boda in
      let th2b = TRANS th1a (EQF_INTRO(NOT_INTRO(DISCH boda th2a))) in
      let th2 = UNDISCH(NOT_ELIM(EQF_ELIM th2b)) in
      let th3 = itlist SIMPLE_CHOOSE evs th2 in
      SPECL avs (MATCH_MP (FINAL_RULE (DISCH_ALL th3)) th1)
    let ideal tms tm =
      let rawvars = itlist grobvars (tm::tms) [] in
      let vars = sort (fun x y -> x < y) (setify rawvars) in
      let pols = map (grobify_term vars) tms 
      let pol = grobify_term vars tm in
      let cert = grobner_ideal vars pols pol in
      map (fun n -> let p = assocd n cert [] in holify_polynomial vars p)
          (0--(length pols-1)) in
    RING,ideal;;

(* ----------------------------------------------------------------------- *)
(* Separate out the cases.                                                 *)
(* ----------------------------------------------------------------------- *)

/// Generic ring procedure.
let RING parms = fst(RING_AND_IDEAL_CONV parms);;

/// Generic procedure to compute cofactors for ideal membership.
let ideal_cofactors parms = snd(RING_AND_IDEAL_CONV parms);;

(* ------------------------------------------------------------------------- *)
(* Simplify a natural number assertion to eliminate conditionals, DIV, MOD,  *)
(* PRE, cutoff subtraction, EVEN and ODD. Try to do it in a way that makes   *)
(* new quantifiers universal. At the moment we don't split "<=>" which would *)
(* make this quantifier selection work there too; better to do NNF first if  *)
(* you care. This also applies to EVEN and ODD.                              *)
(* ------------------------------------------------------------------------- *)

/// Eliminates predecessor, cutoff subtraction, even and odd, division and modulus.
let NUM_SIMPLIFY_CONV =
  let pre_tm = (parse_term @"PRE")
  let div_tm = (parse_term @"(DIV):num->num->num")
  let mod_tm = (parse_term @"(MOD):num->num->num")
  let p_tm = (parse_term @"P:num->bool") 
  let n_tm = (parse_term @"n:num") 
  let m_tm = (parse_term @"m:num")
  let q_tm = (parse_term @"P:num->num->bool") 
  let a_tm = (parse_term @"a:num") 
  let b_tm = (parse_term @"b:num") in
  let is_pre tm = is_comb tm && Choice.get <| rator tm = pre_tm
  let is_sub = is_binop (parse_term @"(-):num->num->num")
  let is_divmod =
    let is_div = is_binop div_tm 
    let is_mod = is_binop mod_tm in
    fun tm -> is_div tm || is_mod tm
  let contains_quantifier =
    /// Tests for failure.
    let can f x = 
        try f x |> ignore; true
        with Failure _ -> false
    
    can (find_term (fun t -> is_forall t || is_exists t || is_uexists t))
  let BETA2_CONV = RATOR_CONV BETA_CONV |>THENC<| BETA_CONV
  let PRE_ELIM_THM'' = CONV_RULE (RAND_CONV NNF_CONV) PRE_ELIM_THM
  let SUB_ELIM_THM'' = CONV_RULE (RAND_CONV NNF_CONV) SUB_ELIM_THM
  let DIVMOD_ELIM_THM'' = CONV_RULE (RAND_CONV NNF_CONV) DIVMOD_ELIM_THM
  let pth_evenodd = 
    prove ((parse_term @"(EVEN(x) <=> (!y. ~(x = SUC(2 * y)))) /\
       (ODD(x) <=> (!y. ~(x = 2 * y))) /\
       (~EVEN(x) <=> (!y. ~(x = 2 * y))) /\
       (~ODD(x) <=> (!y. ~(x = SUC(2 * y))))"),
      REWRITE_TAC[GSYM NOT_EXISTS_THM; GSYM EVEN_EXISTS; GSYM ODD_EXISTS] |>THEN<|
      REWRITE_TAC[NOT_EVEN; NOT_ODD]) in
  let rec NUM_MULTIPLY_CONV pos tm =
    if is_forall tm || is_exists tm || is_uexists tm then
       BINDER_CONV (NUM_MULTIPLY_CONV pos) tm
    elif is_imp tm && contains_quantifier tm then
        COMB2_CONV (RAND_CONV(NUM_MULTIPLY_CONV(not pos))) (NUM_MULTIPLY_CONV pos) tm
    elif (is_conj tm || is_disj tm || is_iff tm) && contains_quantifier tm then
         BINOP_CONV (NUM_MULTIPLY_CONV pos) tm
    elif is_neg tm && not pos && contains_quantifier tm then
       RAND_CONV (NUM_MULTIPLY_CONV (not pos)) tm
    else
       try 
           let t = find_term (fun t -> is_pre t && free_in t tm) tm in
           let ty = Choice.get <| type_of t in
           let v = genvar ty in
           let p = Choice.get <| mk_abs(v,subst [v,t] tm) in
           let th0 = if pos then PRE_ELIM_THM'' else PRE_ELIM_THM' in
           let th1 = INST [p,p_tm; Choice.get <| rand t,n_tm] th0 in
           let th2 = CONV_RULE(COMB2_CONV (RAND_CONV BETA_CONV)
                      (BINDER_CONV(RAND_CONV BETA_CONV))) th1 in
           CONV_RULE(RAND_CONV (NUM_MULTIPLY_CONV pos)) th2
       with 
       | Failure _ -> 
       try
           let t = find_term (fun t -> is_sub t && free_in t tm) tm in
           let ty = Choice.get <| type_of t in
           let v = genvar ty in
           let p = Choice.get <| mk_abs(v,subst [v,t] tm) in
           let th0 = if pos then SUB_ELIM_THM'' else SUB_ELIM_THM' in
           let th1 = INST [p,p_tm; lhand t,a_tm; Choice.get <| rand t,b_tm] th0 in
           let th2 = CONV_RULE(COMB2_CONV (RAND_CONV BETA_CONV) (BINDER_CONV(RAND_CONV BETA_CONV))) th1 in
           CONV_RULE(RAND_CONV (NUM_MULTIPLY_CONV pos)) th2
       with 
       | Failure _ -> 
       try
           let t = find_term (fun t -> is_divmod t && free_in t tm) tm in
           let x = lhand t 
           let y = Choice.get <| rand t in
           let dtm = Choice.get <| mk_comb(Choice.get <| mk_comb(div_tm,x),y)
           let mtm = Choice.get <| mk_comb(Choice.get <| mk_comb(mod_tm,x),y) in
           let vd = genvar(Choice.get <| type_of dtm)
           let vm = genvar(Choice.get <| type_of mtm) in
           let p = list_mk_abs([vd;vm],subst[vd,dtm; vm,mtm] tm) in
           let th0 = if pos then DIVMOD_ELIM_THM'' else DIVMOD_ELIM_THM' in
           let th1 = INST [p,q_tm; x,m_tm; y,n_tm] th0 in
           let th2 = CONV_RULE (COMB2_CONV (RAND_CONV BETA2_CONV) (funpow 2 BINDER_CONV (RAND_CONV BETA2_CONV))) th1 in
           CONV_RULE(RAND_CONV (NUM_MULTIPLY_CONV pos)) th2
       with 
       | Failure _ -> REFL tm in
  NUM_REDUCE_CONV |>THENC<|
  CONDS_CELIM_CONV |>THENC<|
  NNF_CONV |>THENC<|
  NUM_MULTIPLY_CONV true |>THENC<|
  NUM_REDUCE_CONV |>THENC<|
  GEN_REWRITE_CONV ONCE_DEPTH_CONV [pth_evenodd];;

(* ----------------------------------------------------------------------- *)
(* Natural number version of ring procedure with this normalization.       *)
(* ----------------------------------------------------------------------- *)

/// Ring decision procedure instantiated to natural numbers.
let NUM_RING =
  let NUM_INTEGRAL_LEMMA = 
    prove ((parse_term @"(w = x + d) /\ (y = z + e) ==> ((w * y + x * z = w * z + x * y) <=> (w = x) \/ (y = z))"), DISCH_THEN(fun th -> REWRITE_TAC[th]) 
     |>THEN<| REWRITE_TAC[LEFT_ADD_DISTRIB; RIGHT_ADD_DISTRIB; GSYM ADD_ASSOC] 
     |>THEN<| ONCE_REWRITE_TAC [AC ADD_AC (parse_term @"a + b + c + d + e = a + c + e + b + d")] 
     |>THEN<| REWRITE_TAC[EQ_ADD_LCANCEL; EQ_ADD_LCANCEL_0; MULT_EQ_0]) in
  let NUM_INTEGRAL = 
    prove ((parse_term @"(!x. 0 * x = 0) /\
     (!x y z. (x + y = x + z) <=> (y = z)) /\
     (!w x y z. (w * y + x * z = w * z + x * y) <=> (w = x) \/ (y = z))"),
     REWRITE_TAC[MULT_CLAUSES; EQ_ADD_LCANCEL] |>THEN<|
     REPEAT GEN_TAC |>THEN<|
     DISJ_CASES_TAC (SPECL [(parse_term @"w:num"); (parse_term @"x:num")] LE_CASES) |>THEN<|
     DISJ_CASES_TAC (SPECL [(parse_term @"y:num"); (parse_term @"z:num")] LE_CASES) |>THEN<|
     REPEAT(FIRST_X_ASSUM (CHOOSE_THEN SUBST1_TAC << REWRITE_RULE[LE_EXISTS])) |>THEN<|
     ASM_MESON_TAC[NUM_INTEGRAL_LEMMA; ADD_SYM; MULT_SYM]) in
  let rawring =
    RING(dest_numeral,mk_numeral,NUM_EQ_CONV,
         genvar bool_ty,(parse_term @"(+):num->num->num"),genvar bool_ty,
         genvar bool_ty,(parse_term @"(*):num->num->num"),genvar bool_ty,
         (parse_term @"(EXP):num->num->num"),
         NUM_INTEGRAL,TRUTH,NUM_NORMALIZE_CONV) in
  let initconv = NUM_SIMPLIFY_CONV |>THENC<| GEN_REWRITE_CONV DEPTH_CONV [ADD1]
  let t_tm = (parse_term @"T") in
  fun tm -> let th = initconv tm in
            if Choice.get <| rand(concl th) = t_tm then th
            else EQ_MP (SYM th) (rawring(Choice.get <| rand(concl th)));;
