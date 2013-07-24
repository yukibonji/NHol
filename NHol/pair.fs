﻿(*

Copyright 1998 University of Cambridge
Copyright 1998-2007 John Harrison
Copyright 2013 Jack Pappas, Eric Taucher

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
/// Theory of pairs.
module NHol.pair

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
#endif

(* ------------------------------------------------------------------------- *)
(* Constants implementing (or at least tagging) syntactic sugar.             *)
(* ------------------------------------------------------------------------- *)
let LET_DEF = new_definition(parse_term @"LET (f:A->B) x = f x")

let LET_END_DEF = new_definition(parse_term @"LET_END (t:A) = t")

let GABS_DEF = new_definition(parse_term @"GABS (P:A->bool) = (@) P")

let GEQ_DEF = new_definition(parse_term @"GEQ a b = (a:A = b)")

let _SEQPATTERN = 
    new_definition
        (parse_term @"_SEQPATTERN = \r s x. if ?y. r x y then r x else s x")

let _UNGUARDED_PATTERN = 
    new_definition(parse_term @"_UNGUARDED_PATTERN = \p r. p /\ r")

let _GUARDED_PATTERN = 
    new_definition(parse_term @"_GUARDED_PATTERN = \p g r. p /\ g /\ r")

let _MATCH = 
    new_definition
        (parse_term @"_MATCH =  \e r. if (?!) (r e) then (@) (r e) else @z. F")

let _FUNCTION = 
    new_definition
        (parse_term @"_FUNCTION = \r x. if (?!) (r x) then (@) (r x) else @z. F")

(* ------------------------------------------------------------------------- *)
(* Pair type.                                                                *)
(* ------------------------------------------------------------------------- *)
let mk_pair_def = 
    new_definition(parse_term @"mk_pair (x:A) (y:B) = \a b. (a = x) /\ (b = y)")

let PAIR_EXISTS_THM = 
    prove((parse_term @"?x. ?(a:A) (b:B). x = mk_pair a b"), MESON_TAC [])

let prod_tybij = 
    new_type_definition "prod" ("ABS_prod", "REP_prod") PAIR_EXISTS_THM

let REP_ABS_PAIR = 
    prove
        ((parse_term @"!(x:A) (y:B). REP_prod (ABS_prod (mk_pair x y)) = mk_pair x y"), 
         MESON_TAC [prod_tybij])

parse_as_infix(",", (14, "right"))

let COMMA_DEF = new_definition(parse_term @"(x:A),(y:B) = ABS_prod(mk_pair x y)") // try to put a space after ABS_Prod

let FST_DEF = new_definition(parse_term @"FST (p:A#B) = @x. ?y. p = x,y")

let SND_DEF = new_definition(parse_term @"SND (p:A#B) = @y. ?x. p = x,y")

let PAIR_EQ = 
    prove
        ((parse_term @"!(x:A) (y:B) a b. (x,y = a,b) <=> (x = a) /\ (y = b)"), 
         REPEAT GEN_TAC
         |> THEN <| EQ_TAC
         |> THENL <| [REWRITE_TAC [COMMA_DEF]
                      |> THEN 
                      <| DISCH_THEN
                             (MP_TAC 
                              << AP_TERM(parse_term @"REP_prod:A#B->A->B->bool"))
                      |> THEN <| REWRITE_TAC [REP_ABS_PAIR]
                      |> THEN <| REWRITE_TAC [mk_pair_def; FUN_EQ_THM]
                      ALL_TAC]
         |> THEN <| MESON_TAC [])

let PAIR_SURJECTIVE = 
    prove((parse_term @"!p:A#B. ?x y. p = x,y"), GEN_TAC
                                                |> THEN 
                                                <| REWRITE_TAC [COMMA_DEF]
                                                |> THEN 
                                                <| MP_TAC
                                                       (SPEC 
                                                            (parse_term @"REP_prod p :A->B->bool") 
                                                            (CONJUNCT2 
                                                                 prod_tybij))
                                                |> THEN 
                                                <| REWRITE_TAC 
                                                       [CONJUNCT1 prod_tybij]
                                                |> THEN 
                                                <| DISCH_THEN
                                                       (X_CHOOSE_THEN 
                                                            (parse_term @"a:A") 
                                                            (X_CHOOSE_THEN 
                                                                 (parse_term @"b:B") 
                                                                 MP_TAC))
                                                |> THEN 
                                                <| DISCH_THEN
                                                       (MP_TAC 
                                                        << AP_TERM
                                                               (parse_term @"ABS_prod:(A->B->bool)->A#B"))
                                                |> THEN 
                                                <| REWRITE_TAC 
                                                       [CONJUNCT1 prod_tybij]
                                                |> THEN <| DISCH_THEN SUBST1_TAC
                                                |> THEN 
                                                <| MAP_EVERY EXISTS_TAC 
                                                       [(parse_term @"a:A")
                                                        (parse_term @"b:B")]
                                                |> THEN <| REFL_TAC)

let FST = 
    prove
        ((parse_term @"!(x:A) (y:B). FST(x,y) = x"), 
         REPEAT GEN_TAC
         |> THEN <| REWRITE_TAC [FST_DEF]
         |> THEN <| MATCH_MP_TAC SELECT_UNIQUE
         |> THEN <| GEN_TAC
         |> THEN <| BETA_TAC
         |> THEN <| REWRITE_TAC [PAIR_EQ]
         |> THEN <| EQ_TAC
         |> THEN <| STRIP_TAC
         |> THEN <| ASM_REWRITE_TAC []
         |> THEN <| EXISTS_TAC(parse_term @"y:B")
         |> THEN <| ASM_REWRITE_TAC [])

let SND = 
    prove
        ((parse_term @"!(x:A) (y:B). SND(x,y) = y"), 
         REPEAT GEN_TAC
         |> THEN <| REWRITE_TAC [SND_DEF]
         |> THEN <| MATCH_MP_TAC SELECT_UNIQUE
         |> THEN <| GEN_TAC
         |> THEN <| BETA_TAC
         |> THEN <| REWRITE_TAC [PAIR_EQ]
         |> THEN <| EQ_TAC
         |> THEN <| STRIP_TAC
         |> THEN <| ASM_REWRITE_TAC []
         |> THEN <| EXISTS_TAC(parse_term @"x:A")
         |> THEN <| ASM_REWRITE_TAC [])

let PAIR = 
    prove
        ((parse_term @"!x:A#B. FST x,SND x = x"), 
         GEN_TAC
         |> THEN 
         <| (X_CHOOSE_THEN (parse_term @"a:A") 
                 (X_CHOOSE_THEN (parse_term @"b:B") SUBST1_TAC) 
                 (SPEC (parse_term @"x:A#B") PAIR_SURJECTIVE))
         |> THEN <| REWRITE_TAC [FST; SND])

let pair_INDUCT = 
    prove
        ((parse_term @"!P. (!x y. P (x,y)) ==> !p. P p"), 
         REPEAT STRIP_TAC
         |> THEN <| GEN_REWRITE_TAC RAND_CONV [GSYM PAIR]
         |> THEN <| FIRST_ASSUM MATCH_ACCEPT_TAC)

let pair_RECURSION = 
    prove
        ((parse_term @"!PAIR'. ?fn:A#B->C. !a0 a1. fn (a0,a1) = PAIR' a0 a1"), 
         GEN_TAC
         |> THEN <| EXISTS_TAC(parse_term @"\p. (PAIR':A->B->C) (FST p) (SND p)")
         |> THEN <| REWRITE_TAC [FST; SND])

(* ------------------------------------------------------------------------- *)
(* Syntax operations.                                                        *)
(* ------------------------------------------------------------------------- *)
/// Tests a term to see if it is a pair.
let is_pair = is_binary ","

/// Breaks apart a pair into two separate terms.
let dest_pair = Choice.get << dest_binary ","

/// Constructs object-level pair from a pair of terms.
let mk_pair = 
    let ptm = Choice.get <| mk_const(",", [])
    fun (l, r) -> 
        Choice.get <| mk_comb(Choice.get <| mk_comb(Choice.get <| inst [Choice.get <| type_of l, aty; Choice.get <| type_of r, bty] ptm, l), r)

//extend_basic_rewrites [FST; SND; PAIR] duplicate line

(* ------------------------------------------------------------------------- *)
(* Extend basic rewrites; extend new_definition to allow paired varstructs.  *)
(* ------------------------------------------------------------------------- *)

extend_basic_rewrites [FST; SND; PAIR] // deleted ;;

(* ------------------------------------------------------------------------- *)
(* Extend definitions to paired varstructs with benignity checking.          *)
(* ------------------------------------------------------------------------- *)
/// List of all definitions introduced so far.
let the_definitions = 
    ref 
        [SND_DEF; FST_DEF; COMMA_DEF; mk_pair_def; GEQ_DEF; GABS_DEF; 
         LET_END_DEF; LET_DEF; one_DEF; I_DEF; o_DEF; COND_DEF; _FALSITY_; 
         EXISTS_UNIQUE_DEF; NOT_DEF; F_DEF; OR_DEF; EXISTS_DEF; FORALL_DEF; 
         IMP_DEF; AND_DEF; T_DEF]

/// Declare a new constant and a definitional axiom.
let new_definition = 
    let depair = 
        let rec depair gv arg = 
            try 
                let l, r = dest_pair arg
                (depair (Choice.get <| list_mk_icomb "FST" [gv]) l) @ (depair (Choice.get <| list_mk_icomb "SND" [gv]) r) //deleted new line before @
            with
            | Failure _ -> [gv, arg]
        fun arg -> 
            let gv = genvar(Choice.get <| type_of arg)
            gv, depair gv arg
    fun tm -> 
        let avs, def = strip_forall tm
        try 
            let th, th' = 
                tryfind (fun th -> Some (th, PART_MATCH I th def)) (!the_definitions)
                |> Option.getOrFailWith "tryfind"
            ignore(PART_MATCH I th' (snd(strip_forall(concl th))))
            warn true "Benign redefinition"
            GEN_ALL(GENL avs th')
        with
        | Failure _ -> 
            let l, r = Choice.get <| dest_eq def
            let fn, args = strip_comb l
            let gargs, reps = (I ||>> unions)(unzip(map depair args))
            let l' = list_mk_comb(fn, gargs)
            let r' = Choice.get <| subst reps r
            let th1 = new_definition(Choice.get <| mk_eq(l', r'))
            let slist = zip args gargs
            let th2 = INST slist (SPEC_ALL th1)
            let xreps = map (Choice.get << subst slist << fst) reps
            let threps = map (SYM << PURE_REWRITE_CONV [FST; SND]) xreps
            let th3 = TRANS th2 (SYM(SUBS_CONV threps r))
            let th4 = GEN_ALL(GENL avs th3)
            the_definitions := th4 :: (!the_definitions) // to be checked
            th4

(* ------------------------------------------------------------------------- *)
(* A few more useful definitions.                                            *)
(* ------------------------------------------------------------------------- *)
let CURRY_DEF = new_definition(parse_term @"CURRY(f:A#B->C) x y = f(x,y)")

let UNCURRY_DEF = 
    new_definition(parse_term @"!f x y. UNCURRY(f:A->B->C)(x,y) = f x y")

let PASSOC_DEF = 
    new_definition
        (parse_term @"!f x y z. PASSOC (f:(A#B)#C->D) (x,y,z) = f ((x,y),z)")

(* ------------------------------------------------------------------------- *)
(* Analog of ABS_CONV for generalized abstraction.                           *)
(* ------------------------------------------------------------------------- *)
/// Applies a conversion to the Choice.get <| body of a generalized abstraction.
let GABS_CONV conv tm = 
    if is_abs tm
    then ABS_CONV conv tm
    else 
        let gabs, bod = Choice.get <| dest_comb tm
        let f, qtm = Choice.get <| dest_abs bod
        let xs, bod = strip_forall qtm
        AP_TERM gabs (ABS f (itlist MK_FORALL xs (RAND_CONV conv bod)))

(* ------------------------------------------------------------------------- *)
(* General beta-conversion over linear pattern of nested constructors.       *)
(* ------------------------------------------------------------------------- *)
/// Beta-reduces general beta-redexes (e.g. paired ones).
let GEN_BETA_CONV = 
    let projection_cache = ref []
    let create_projections conname =
        match assoc conname !projection_cache with
        | Some x -> x
        | None ->
            let genty = Choice.get <| get_const_type conname
            let conty = fst(Choice.get <| dest_type(repeat (snd << (Choice.get << dest_fun_ty)) genty))
            let _, _, rth =
                assoc conty (!inductive_type_store)
                |> Option.getOrFailWith "find"
            let sth = SPEC_ALL rth
            let evs, bod = strip_exists(concl sth)
            let cjs = conjuncts bod
            let ourcj = 
                find 
                    ((=) conname << fst << Choice.get << dest_const << fst << strip_comb //check (=) conname, check all better
                     << Choice.get << rand << lhand << snd << strip_forall) cjs
            let n = index ourcj cjs
            let avs, eqn = strip_forall ourcj
            let con', args = strip_comb(Choice.get <| rand eqn)
            let aargs, zargs = chop_list (length avs) args
            let gargs = map (genvar << Choice.get << type_of) zargs
            let gcon = 
                genvar(itlist ((fun ty -> Choice.get << mk_fun_ty ty) << Choice.get << type_of) avs (Choice.get <| type_of(Choice.get <| rand eqn)))
            let bth = 
                INST [list_mk_abs(aargs @ gargs, list_mk_comb(gcon, avs)), con'] 
                    sth
            let cth = el n (CONJUNCTS(ASSUME(snd(strip_exists(concl bth)))))
            let dth = 
                CONV_RULE 
                    (funpow (length avs) BINDER_CONV (RAND_CONV(BETAS_CONV))) 
                    cth
            let eth = 
                SIMPLE_EXISTS (Choice.get <| rator(lhand(snd(strip_forall(concl dth))))) dth
            let fth = PROVE_HYP bth (itlist SIMPLE_CHOOSE evs eth)
            let zty = Choice.get <| type_of(Choice.get <| rand(snd(strip_forall(concl dth))))
            let mk_projector a = 
                let ity = Choice.get <| type_of a
                let th = 
                    BETA_RULE(PINST [ity, zty] [list_mk_abs(avs, a), gcon] fth)
                SYM(SPEC_ALL(SELECT_RULE th))
            let ths = map mk_projector avs
            (projection_cache := (conname, ths) :: (!projection_cache)
             ths)
    let GEQ_CONV = REWR_CONV(GSYM GEQ_DEF)
    let DEGEQ_RULE = CONV_RULE(REWR_CONV GEQ_DEF)
    let GABS_RULE = 
        let pth = 
            prove
                ((parse_term @"(?) P ==> P (GABS P)"), 
                 SIMP_TAC [GABS_DEF; SELECT_AX; ETA_AX])
        MATCH_MP pth
    let rec create_iterated_projections tm = 
        if frees tm = []
        then []
        elif is_var tm
        then [REFL tm]
        else 
            let con, args = strip_comb tm
            let prjths = create_projections(fst(Choice.get <| dest_const con))
            let atm = Choice.get <| rand(Choice.get <| rand(concl(hd prjths)))
            let instn = term_match [] atm tm
            let arths = map (INSTANTIATE instn) prjths
            let ths = 
                map (fun arth -> 
                        let sths = 
                            create_iterated_projections(lhand(concl arth))
                        map (CONV_RULE(RAND_CONV(SUBS_CONV [arth]))) sths) arths
            unions' equals_thm ths
    let GEN_BETA_CONV1 tm = //I don't know if using the same name of the function to be defined can cause problems
        try 
            BETA_CONV tm
        with
        | Failure _ -> 
            let l, r = Choice.get <| dest_comb tm
            let vstr, bod = dest_gabs l
            let instn = term_match [] vstr r
            let prjs = create_iterated_projections vstr
            let th1 = SUBS_CONV prjs bod
            let bod' = Choice.get <| rand(concl th1)
            let gv = genvar(Choice.get <| type_of vstr)
            let pat = Choice.get <| mk_abs(gv, Choice.get <| subst [gv, vstr] bod')
            let th2 = TRANS (BETA_CONV(Choice.get <| mk_comb(pat, vstr))) (SYM th1)
            let avs = fst(strip_forall(Choice.get <| body(Choice.get <| rand l)))
            let th3 = GENL (fst(strip_forall(Choice.get <| body(Choice.get <| rand l)))) th2
            let efn = genvar(Choice.get <| type_of pat)
            let th4 = 
                EXISTS (mk_exists(efn, Choice.get <| subst [efn, pat] (concl th3)), pat) th3
            let th5 = 
                CONV_RULE (funpow (length avs + 1) BINDER_CONV GEQ_CONV) th4
            let th6 = CONV_RULE BETA_CONV (GABS_RULE th5)
            INSTANTIATE instn (DEGEQ_RULE(SPEC_ALL th6))
    GEN_BETA_CONV1


(* ------------------------------------------------------------------------- *)
(* Add this to the basic "rewrites" and pairs to the inductive type store.   *)
(* ------------------------------------------------------------------------- *)

extend_basic_convs ("GEN_BETA_CONV", ((parse_term @"GABS (\a. b) c"), GEN_BETA_CONV))

inductive_type_store := ("prod", (1, pair_INDUCT, pair_RECURSION)) :: (!inductive_type_store)

(* ------------------------------------------------------------------------- *)
(* Convenient rules to eliminate binders over pairs.                         *)
(* ------------------------------------------------------------------------- *)
let FORALL_PAIR_THM = 
    prove((parse_term @"!P. (!p. P p) <=> (!p1 p2. P(p1,p2))"), MESON_TAC [PAIR])

let EXISTS_PAIR_THM = 
    prove((parse_term @"!P. (?p. P p) <=> ?p1 p2. P(p1,p2)"), MESON_TAC [PAIR])

// Error unsolved goal
let LAMBDA_PAIR_THM = 
#if BUGGY
    prove
        ((parse_term @"!t. (\p. t p) = (\(x,y). t(x,y))"), 
         REWRITE_TAC [FORALL_PAIR_THM; FUN_EQ_THM])
#else
    Choice1Of2 <| Sequent([],parse_term @"!t. (\p. t p) = (\(x,y). t(x,y))") : thm
#endif

// Error unsolved goal
let PAIRED_ETA_THM = 
#if BUGGY
    prove
        ((parse_term @"(!f. (\(x,y). f (x,y)) = f) /\
    (!f. (\(x,y,z). f (x,y,z)) = f) /\
    (!f. (\(w,x,y,z). f (w,x,y,z)) = f)"), 
         REPEAT STRIP_TAC
         |> THEN <| REWRITE_TAC [FUN_EQ_THM; FORALL_PAIR_THM])
#else
    Choice1Of2 <| Sequent([],parse_term @"(!f. (\(x,y). f (x,y)) = f) /\ (!f. (\(x,y,z). f (x,y,z)) = f) /\ (!f. (\(w,x,y,z). f (w,x,y,z)) = f)")
#endif

// Error unsolved goal
let FORALL_UNCURRY = 
#if BUGGY
    prove
        ((parse_term @"!P. (!f:A->B->C. P f) <=> (!f. P (\a b. f(a,b)))"), 
         GEN_TAC
         |> THEN <| EQ_TAC
         |> THEN <| SIMP_TAC []
         |> THEN <| DISCH_TAC
         |> THEN <| X_GEN_TAC(parse_term @"f:A->B->C")
         |> THEN 
         <| FIRST_ASSUM(MP_TAC << SPEC(parse_term @"\(a,b). (f:A->B->C) a b"))
         |> THEN <| SIMP_TAC [ETA_AX])
#else
    Choice1Of2 <| Sequent([], parse_term @"!P. (!f:A->B->C. P f) <=> (!f. P (\a b. f(a,b)))")
#endif

let EXISTS_UNCURRY = 
    prove
        ((parse_term @"!P. (?f:A->B->C. P f) <=> (?f. P (\a b. f(a,b)))"), 
         ONCE_REWRITE_TAC [MESON [] (parse_term @"(?x. P x) <=> ~(!x. ~P x)")]
         |> THEN <| REWRITE_TAC [FORALL_UNCURRY])

let EXISTS_CURRY = 
    prove
        ((parse_term @"!P. (?f. P f) <=> (?f. P (\(a,b). f a b))"), 
         REWRITE_TAC [EXISTS_UNCURRY; PAIRED_ETA_THM])

let FORALL_CURRY = 
    prove
        ((parse_term @"!P. (!f. P f) <=> (!f. P (\(a,b). f a b))"), 
         REWRITE_TAC [FORALL_UNCURRY; PAIRED_ETA_THM])

(* ------------------------------------------------------------------------- *)
(* Related theorems for explicitly paired quantifiers.                       *)
(* ------------------------------------------------------------------------- *)

// Error unsolved goal
let FORALL_PAIRED_THM = 
#if BUGGY
    prove
        ((parse_term @"!P. (!(x,y). P x y) <=> (!x y. P x y)"), 
         GEN_TAC
         |> THEN <| GEN_REWRITE_TAC (LAND_CONV << RATOR_CONV) [FORALL_DEF]
         |> THEN <| REWRITE_TAC [FUN_EQ_THM; FORALL_PAIR_THM])
#else
    Choice1Of2 <| Sequent([], parse_term @"!P. (!(x,y). P x y) <=> (!x y. P x y)") : thm
#endif

// Error unsolved goal
let EXISTS_PAIRED_THM = 
#if BUGGY
    prove
        ((parse_term @"!P. (?(x,y). P x y) <=> (?x y. P x y)"), 
         GEN_TAC
         |> THEN <| MATCH_MP_TAC(TAUT(parse_term @"(~p <=> ~q) ==> (p <=> q)"))
         |> THEN <| REWRITE_TAC [REWRITE_RULE [ETA_AX] NOT_EXISTS_THM
                                 FORALL_PAIR_THM])
#else
    Choice1Of2 <| Sequent([], parse_term @"!P. (?(x,y). P x y) <=> (?x y. P x y)") : thm
#endif

(* ------------------------------------------------------------------------- *)
(* Likewise for tripled quantifiers (could continue with the same proof).    *)
(* ------------------------------------------------------------------------- *)

// Error unsolved goal
let FORALL_TRIPLED_THM = 
#if BUGGY
    prove
        ((parse_term @"!P. (!(x,y,z). P x y z) <=> (!x y z. P x y z)"), 
         GEN_TAC
         |> THEN <| GEN_REWRITE_TAC (LAND_CONV << RATOR_CONV) [FORALL_DEF]
         |> THEN <| REWRITE_TAC [FUN_EQ_THM; FORALL_PAIR_THM])
#else
    Choice1Of2 <| Sequent([], parse_term @"!P. (!(x,y,z). P x y z) <=> (!x y z. P x y z)") : thm
#endif

// Error unsolved goal
let EXISTS_TRIPLED_THM = 
#if BUGGY
    prove
        ((parse_term @"!P. (?(x,y,z). P x y z) <=> (?x y z. P x y z)"), 
         GEN_TAC
         |> THEN <| MATCH_MP_TAC(TAUT(parse_term @"(~p <=> ~q) ==> (p <=> q)"))
         |> THEN <| REWRITE_TAC [REWRITE_RULE [ETA_AX] NOT_EXISTS_THM
                                 FORALL_PAIR_THM])
#else
    Choice1Of2 <| Sequent([], parse_term @"!P. (!(x,y,z). P x y z) <=> (!x y z. P x y z)") : thm
#endif

(* ------------------------------------------------------------------------- *)
(* Expansion of a let-term.                                                  *)
(* ------------------------------------------------------------------------- *)
/// Evaluates let-terms in the HOL logic.
let let_CONV = 
    let let1_CONV = REWR_CONV LET_DEF
                    |> THENC <| GEN_BETA_CONV
    let lete_CONV = REWR_CONV LET_END_DEF
    let rec EXPAND_BETAS_CONV tm = 
        let tm' = Choice.get <| rator tm
        try 
            let1_CONV tm
        with
        | Failure _ -> 
            let th1 = AP_THM (EXPAND_BETAS_CONV tm') (Choice.get <| rand tm)
            let th2 = GEN_BETA_CONV(Choice.get <| rand(concl th1))
            TRANS th1 th2
    fun tm -> 
        let ltm, pargs = strip_comb tm
        if fst(Choice.get <| dest_const ltm) <> "LET" || pargs = []
        then failwith "let_CONV"
        else 
            let abstm = hd pargs
            let vs, bod = strip_gabs abstm
            let es = tl pargs
            let n = length es
            if length vs <> n
            then failwith "let_CONV"
            else (EXPAND_BETAS_CONV
                  |> THENC <| lete_CONV) tm

/// Eliminates a let binding in a goal by introducing equational assumptions.
let (LET_TAC : tactic) = 
    let is_trivlet tm = 
        try 
            let assigs, bod = dest_let tm
            forall (uncurry (=)) assigs
        with
        | Failure _ -> false
    let PROVE_DEPAIRING_EXISTS = 
        let pth = 
            prove
                ((parse_term @"((x,y) = a) <=> (x = FST a) /\ (y = SND a)"), 
                 MESON_TAC [PAIR; PAIR_EQ])
        let rewr1_CONV = GEN_REWRITE_CONV TOP_DEPTH_CONV [pth]
        let rewr2_RULE = 
            GEN_REWRITE_RULE (LAND_CONV << DEPTH_CONV) 
                [TAUT(parse_term @"(x = x) <=> T")
                 TAUT(parse_term @"a /\ T <=> a")]
        fun tm -> 
            let th1 = rewr1_CONV tm
            let tm1 = Choice.get <| rand(concl th1)
            let cjs = conjuncts tm1
            let vars = map lhand cjs
            let th2 = EQ_MP (SYM th1) (ASSUME tm1)
            let th3 = DISCH_ALL(itlist SIMPLE_EXISTS vars th2)
            let th4 = INST (map (fun t -> Choice.get <| rand t, lhand t) cjs) th3
            MP (rewr2_RULE th4) TRUTH
    fun (asl, w as gl) ->  
            let path = 
                try 
                    find_path is_trivlet w
                with
                | Failure _ -> find_path is_let w
            let tm = follow_path path w
            let assigs, bod = dest_let tm
            let abbrevs = 
                mapfilter (fun (x, y) -> 
                        if x = y
                        then fail()
                        else Choice.get <| mk_eq(x, y)) assigs
            let lvars = itlist (union << frees << lhs) abbrevs []
            let avoids = itlist (union << thm_frees << snd) asl (frees w)
            let rename = Choice.get << vsubst(zip (Choice.get <| variants avoids lvars) lvars)
            let abbrevs' = 
                map (fun eq -> 
                        let l, r = Choice.get <| dest_eq eq
                        Choice.get <| mk_eq(rename l, r)) abbrevs
            let deprths = map PROVE_DEPAIRING_EXISTS abbrevs'
            (MAP_EVERY (REPEAT_TCL CHOOSE_THEN (fun th -> 
                                let th' = SYM th
                                SUBST_ALL_TAC th'
                                |> THEN <| ASSUME_TAC th')) deprths
             |> THEN <| W(fun (asl', w') -> 
                                let tm' = follow_path path w'
                                CONV_TAC(PATH_CONV path (K(let_CONV tm'))))) gl
