﻿(*

Copyright 1998 University of Cambridge
Copyright 1998-2007 John Harrison
Copyright 2012 Marco Maggesi
Copyright 2012 Vincent Aravantinos
Copyright 2013 Jack Pappas, Anh-Dung Phan

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

/// Preterms and pretypes; typechecking; translation to types and terms.
module NHol.preterm

open FSharp.Compatibility.OCaml
open FSharp.Compatibility.OCaml.Num
open NHol.lib
open NHol.fusion
open NHol.fusion.Hol_kernel
open NHol.basics
open NHol.nets
open NHol.printer

(* ------------------------------------------------------------------------- *)
(* Flag to say whether to treat varstruct "\const. bod" as variable.         *)
(* ------------------------------------------------------------------------- *)

let ignore_constant_varstruct = ref true

(* ------------------------------------------------------------------------- *)
(* Flags controlling the treatment of invented type variables in quotations. *)
(* It can be treated as an error, result in a warning, ||  neither of those.  *)
(* ------------------------------------------------------------------------- *)

let type_invention_warning = ref true
let type_invention_error = ref false

(* ------------------------------------------------------------------------- *)
(* Implicit types ||  type schemes for non-constants.                         *)
(* ------------------------------------------------------------------------- *)

let the_implicit_types = ref([] : (string * hol_type) list)

(* ------------------------------------------------------------------------- *)
(* Overloading and interface mapping.                                        *)
(* ------------------------------------------------------------------------- *)

let make_overloadable s gty = 
    if can (assoc s) (!the_overload_skeletons)
    then 
        if assoc s (!the_overload_skeletons) = gty
        then ()
        else failwith "make_overloadable: differs from existing skeleton"
    else the_overload_skeletons := (s, gty) :: (!the_overload_skeletons)

let remove_interface sym = 
    let ``interface`` = filter ((<>) sym << fst) (!the_interface)
    the_interface := ``interface``

let reduce_interface(sym, tm) = 
    let namty = 
        try 
            dest_const tm
        with
        | Failure _ -> dest_var tm
    the_interface := filter ((<>)(sym, namty)) (!the_interface)

let override_interface(sym, tm) = 
    let namty = 
        try 
            dest_const tm
        with
        | Failure _ -> dest_var tm
    let ``interface`` = filter ((<>) sym << fst) (!the_interface)
    the_interface := (sym, namty) :: ``interface``

let overload_interface(sym, tm) = 
    let gty = 
        try 
            assoc sym (!the_overload_skeletons)
        with
        | Failure _ -> failwith("symbol \"" ^ sym ^ "\" is not overloadable")
    let (name, ty) as namty = 
        try 
            dest_const tm
        with
        | Failure _ -> dest_var tm
    if not(can (type_match gty ty) [])
    then failwith "Not an instance of type skeleton"
    else 
        let ``interface`` = filter ((<>)(sym, namty)) (!the_interface)
        the_interface := (sym, namty) :: ``interface``

let prioritize_overload ty = 
    do_list (fun (s, gty) -> 
            try 
                let _, (n, t) = 
                    find (fun (s', _) -> 
                            match _arg1 with
                            | n, t -> s' = s && mem ty (map fst (type_match gty t []))) (!the_interface)
                overload_interface(s, mk_var(n, t))
            with
            | Failure _ -> ()) (!the_overload_skeletons)

(* ------------------------------------------------------------------------- *)
(* Type abbreviations.                                                       *)
(* ------------------------------------------------------------------------- *)

let new_type_abbrev, remove_type_abbrev, type_abbrevs = 
    let the_type_abbreviations = ref([] : (string * hol_type) list)
    let remove_type_abbrev s = the_type_abbreviations := filter (fun (s', _) -> s' <> s) (!the_type_abbreviations)
    let new_type_abbrev(s, ty) = 
        (remove_type_abbrev s
         the_type_abbreviations := merge (<) [s, ty] (!the_type_abbreviations))
    let type_abbrevs() = !the_type_abbreviations
    new_type_abbrev, remove_type_abbrev, type_abbrevs

(* ------------------------------------------------------------------------- *)
(* Handle constant hiding.                                                   *)
(* ------------------------------------------------------------------------- *)

let hide_constant, unhide_constant, is_hidden = 
    let hcs = ref([] : string list)
    let hide_constant c = hcs := union [c] (!hcs)
    let unhide_constant c = hcs := subtract (!hcs) [c]
    let is_hidden c = mem c (!hcs)
    hide_constant, unhide_constant, is_hidden

(* ------------------------------------------------------------------------- *)
(* The type of pretypes.                                                     *)
(* ------------------------------------------------------------------------- *)

type pretype = 
    | Utv of string                   (* User type variable         *)
    | Ptycon of string * pretype list (* Type constructor           *)
    | Stv of int                      (* System type variable       *)

(* ------------------------------------------------------------------------- *)
(* Dummy pretype for the parser to stick in before a proper typing pass.     *)
(* ------------------------------------------------------------------------- *)

let dpty = Ptycon("", [])

(* ------------------------------------------------------------------------- *)
(* Convert type to pretype.                                                  *)
(* ------------------------------------------------------------------------- *)

let rec pretype_of_type ty = 
    try 
        let con, args = dest_type ty
        Ptycon(con, map pretype_of_type args)
    with
    | Failure _ -> Utv(dest_vartype ty)

(* ------------------------------------------------------------------------- *)
(* Preterm syntax.                                                           *)
(* ------------------------------------------------------------------------- *)

type preterm = 
    | Varp of string * pretype    (* Variable           - v      *)
    | Constp of string * pretype  (* Constant           - c      *)
    | Combp of preterm * preterm  (* Combination        - f x    *)
    | Absp of preterm * preterm   (* Lambda-abstraction - \x. t  *)
    | Typing of preterm * pretype (* Type constraint    - t : ty *)

(* ------------------------------------------------------------------------- *)
(* Convert term to preterm.                                                  *)
(* ------------------------------------------------------------------------- *)

let rec preterm_of_term tm = 
    try 
        let n, ty = dest_var tm
        Varp(n, pretype_of_type ty)
    with
    | Failure _ -> 
        try 
            let n, ty = dest_const tm
            Constp(n, pretype_of_type ty)
        with
        | Failure _ -> 
            try 
                let v, bod = dest_abs tm
                Absp(preterm_of_term v, preterm_of_term bod)
            with
            | Failure _ -> 
                let l, r = dest_comb tm
                Combp(preterm_of_term l, preterm_of_term r)

(* ------------------------------------------------------------------------- *)
(* Main pretype->type, preterm->term and retypechecking functions.           *)
(* ------------------------------------------------------------------------- *)

let type_of_pretype, term_of_preterm, retypecheck = 
    let tyv_num = ref 0
    let new_type_var() = 
        let n = !tyv_num
        (tyv_num := n + 1
         Stv(n))
    let pmk_cv(s, pty) = 
        if can get_const_type s
        then Constp(s, pty)
        else Varp(s, pty)
    let pmk_numeral = 
        let num_pty = Ptycon("num", [])
        let NUMERAL = Constp("NUMERAL", Ptycon("fun", [num_pty; num_pty]))
        let BIT0 = Constp("BIT0", Ptycon("fun", [num_pty; num_pty]))
        let BIT1 = Constp("BIT1", Ptycon("fun", [num_pty; num_pty]))
        let t_0 = Constp("_0", num_pty)
        let rec pmk_numeral(n) = 
            if n =/ num_0
            then t_0
            else 
                let m = quo_num n (num_2)
                let b = mod_num n (num_2)
                let op = 
                    if b =/ num_0
                    then BIT0
                    else BIT1
                Combp(op, pmk_numeral(m))
        fun n -> Combp(NUMERAL, pmk_numeral n)
    
    (* ----------------------------------------------------------------------- *)
    (* Pretype substitution for a pretype resulting from translation of type.  *)
    (* ----------------------------------------------------------------------- *)
    
    let rec pretype_subst th ty = 
        match ty with
        | Ptycon(tycon, args) -> Ptycon(tycon, map (pretype_subst th) args)
        | Utv v -> rev_assocd ty th ty
        | _ -> failwith "pretype_subst: Unexpected form of pretype"
    
    (* ----------------------------------------------------------------------- *)
    (* Convert type to pretype with new Stvs for all type variables.           *)
    (* ----------------------------------------------------------------------- *)
    
    let pretype_instance ty = 
        let gty = pretype_of_type ty
        let tyvs = map pretype_of_type (tyvars ty)
        let subs = map (fun tv -> new_type_var(), tv) tyvs
        pretype_subst subs gty
    
    (* ----------------------------------------------------------------------- *)
    (* Get a new instance of a constant's generic type modulo interface.       *)
    (* ----------------------------------------------------------------------- *)
    
    let get_generic_type cname = 
        match filter ((=) cname << fst) (!the_interface) with
        | [_, (c, ty)] -> ty
        | _ :: _ :: _ -> assoc cname (!the_overload_skeletons)
        | [] -> get_const_type cname
    (* ----------------------------------------------------------------------- *)
    (* Get the implicit generic type of a variable.                            *)
    (* ----------------------------------------------------------------------- *)
    let get_var_type vname = assoc vname !the_implicit_types
    
    (* ----------------------------------------------------------------------- *)
    (* Unravel unifications and apply them to a type.                          *)
    (* ----------------------------------------------------------------------- *)
    
    let rec solve env pty = 
        match pty with
        | Ptycon(f, args) -> Ptycon(f, map (solve env) args)
        | Stv(i) -> 
            if defined env i
            then solve env (apply env i)
            else pty
        | _ -> pty
    
    (* ----------------------------------------------------------------------- *)
    (* Functions for display of preterms and pretypes, by converting them      *)
    (* to terms and types then re-using standard printing functions.           *)
    (* ----------------------------------------------------------------------- *)
    
    let free_stvs = 
        let rec free_stvs = 
            function 
            | Stv n -> [n]
            | Utv _ -> []
            | Ptycon(_, args) -> flat(map free_stvs args)
        setify << free_stvs
    let string_of_pretype stvs = 
        let rec type_of_pretype' ns = 
            function 
            | Stv n -> 
                mk_vartype(if mem n ns
                           then "?" ^ string_of_int n
                           else "_")
            | Utv v -> mk_vartype v
            | Ptycon(con, args) -> mk_type(con, map (type_of_pretype' ns) args)
        string_of_type << type_of_pretype' stvs
    let string_of_preterm = 
        let rec untyped_t_of_pt = 
            function 
            | Varp(s, pty) -> mk_var(s, aty)
            | Constp(s, pty) -> mk_mconst(s, get_const_type s)
            | Combp(l, r) -> mk_comb(untyped_t_of_pt l, untyped_t_of_pt r)
            | Absp(v, bod) -> mk_gabs(untyped_t_of_pt v, untyped_t_of_pt bod)
            | Typing(ptm, pty) -> untyped_t_of_pt ptm
        string_of_term << untyped_t_of_pt
    let string_of_ty_error env = 
        function 
        | None -> "unify: types cannot be unified " ^ "(you should not see this message, please report)"
        | Some(t, ty1, ty2) -> 
            let ty1 = solve env ty1
            let ty2 = solve env ty2
            let sty1 = string_of_pretype (free_stvs ty2) ty1
            let sty2 = string_of_pretype (free_stvs ty1) ty2
            let default_msg s = " " ^ s ^ " cannot have type " ^ sty1 ^ " and " ^ sty2 ^ " simultaneously"
            match t with
            | Constp(s, _) -> 
                " " ^ s ^ " has type " ^ string_of_type(get_const_type s) ^ ", " ^ "it cannot be used with type " ^ sty2
            | Varp(s, _) -> default_msg s
            | t -> default_msg(string_of_preterm t)
    
    (* ----------------------------------------------------------------------- *)
    (* Unification of types                                                    *)
    (* ----------------------------------------------------------------------- *)
    
    let rec istrivial ptm env x = 
        function 
        | Stv y as t -> y = x || defined env y && istrivial ptm env x (apply env y)
        | Ptycon(f, args) as t when exists (istrivial ptm env x) args -> failwith(string_of_ty_error env ptm)
        | (Ptycon _ | Utv _) -> false
    let unify ptm env ty1 ty2 = 
        let rec unify env = 
            function 
            | [] -> env
            | (ty1, ty2, _) :: oth when ty1 = ty2 -> unify env oth
            | (Ptycon(f, fargs), Ptycon(g, gargs), ptm) :: oth -> 
                if f = g && length fargs = length gargs
                then unify env (map2 (fun x y -> x, y, ptm) fargs gargs @ oth)
                else failwith(string_of_ty_error env ptm)
            | (Stv x, t, ptm) :: oth -> 
                if defined env x
                then unify env ((apply env x, t, ptm) :: oth)
                else 
                    unify (if istrivial ptm env x t
                           then env
                           else (x |-> t) env) oth
            | (t, Stv x, ptm) :: oth -> unify env ((Stv x, t, ptm) :: oth)
            | (_, _, ptm) :: oth -> failwith(string_of_ty_error env ptm)
        unify env [ty1, ty2, match ptm with
                             | None -> None
                             | Some t -> Some(t, ty1, ty2)]
    
    (* ----------------------------------------------------------------------- *)
    (* Attempt to attach a given type to a term, performing unifications.      *)
    (* ----------------------------------------------------------------------- *)
    
    let rec typify ty (ptm, venv, uenv) = 
        match ptm with
        | Varp(s, _) when can (assoc s) venv -> 
            let ty' = assoc s venv
            Varp(s, ty'), [], unify (Some ptm) uenv ty' ty
        | Varp(s, _) when can num_of_string s -> 
            let t = pmk_numeral(num_of_string s)
            let ty' = Ptycon("num", [])
            t, [], unify (Some ptm) uenv ty' ty
        | Varp(s, _) -> 
            warn (s <> "" && isnum s) "Non-numeral begins with a digit"
            if not(is_hidden s) && can get_generic_type s
            then 
                let pty = pretype_instance(get_generic_type s)
                let ptm = Constp(s, pty)
                ptm, [], unify (Some ptm) uenv pty ty
            else 
                let ptm = Varp(s, ty)
                if not(can get_var_type s)
                then ptm, [s, ty], uenv
                else 
                    let pty = pretype_instance(get_var_type s)
                    ptm, [s, ty], unify (Some ptm) uenv pty ty
        | Combp(f, x) -> 
            let ty'' = new_type_var()
            let ty' = Ptycon("fun", [ty''; ty])
            let f', venv1, uenv1 = typify ty' (f, venv, uenv)
            let x', venv2, uenv2 = typify ty'' (x, venv1 @ venv, uenv1)
            Combp(f', x'), (venv1 @ venv2), uenv2
        | Typing(tm, pty) -> typify ty (tm, venv, unify (Some tm) uenv ty pty)
        | Absp(v, bod) -> 
            let ty', ty'' = 
                match ty with
                | Ptycon("fun", [ty'; ty'']) -> ty', ty''
                | _ -> new_type_var(), new_type_var()
            let ty''' = Ptycon("fun", [ty'; ty''])
            let uenv0 = unify (Some ptm) uenv ty''' ty
            let v', venv1, uenv1 = 
                let v', venv1, uenv1 = typify ty' (v, [], uenv0)
                match v' with
                | Constp(s, _) when !ignore_constant_varstruct -> Varp(s, ty'), [s, ty'], uenv0
                | _ -> v', venv1, uenv1
            let bod', venv2, uenv2 = typify ty'' (bod, venv1 @ venv, uenv1)
            Absp(v', bod'), venv2, uenv2
        | _ -> failwith "typify: unexpected constant at this stage"
    
    (* ----------------------------------------------------------------------- *)
    (* Further specialize type constraints by resolving overloadings.          *)
    (* ----------------------------------------------------------------------- *)
    
    let rec resolve_interface ptm cont env = 
        match ptm with
        | Combp(f, x) -> resolve_interface f (resolve_interface x cont) env
        | Absp(v, bod) -> resolve_interface v (resolve_interface bod cont) env
        | Varp(_, _) -> cont env
        | Constp(s, ty) -> 
            let maps = filter (fun (s', _) -> s' = s) (!the_interface)
            if maps = []
            then cont env
            else 
                tryfind (fun (_, _) -> 
                        match _arg3 with
                        | _, ty' -> 
                            let ty' = pretype_instance ty'
                            cont(unify (Some ptm) env ty' ty)) maps
    
    (* ----------------------------------------------------------------------- *)
    (* Hence apply throughout a preterm.                                       *)
    (* ----------------------------------------------------------------------- *)
    
    let rec solve_preterm env ptm = 
        match ptm with
        | Varp(s, ty) -> Varp(s, solve env ty)
        | Combp(f, x) -> Combp(solve_preterm env f, solve_preterm env x)
        | Absp(v, bod) -> Absp(solve_preterm env v, solve_preterm env bod)
        | Constp(s, ty) -> 
            let tys = solve env ty
            try 
                let _, (c', _) = 
                    find (fun (s', _) -> 
                            match _arg4 with
                            | c', ty' -> s = s' && can (unify None env (pretype_instance ty')) ty) (!the_interface)
                pmk_cv(c', tys)
            with
            | Failure _ -> Constp(s, tys)
    
    (* ----------------------------------------------------------------------- *)
    (* Flag to indicate that Stvs were translated to real type variables.      *)
    (* ----------------------------------------------------------------------- *)
    
    let stvs_translated = ref false
    
    (* ----------------------------------------------------------------------- *)
    (* Pretype <-> type conversion; -> flags system type variable translation. *)
    (* ----------------------------------------------------------------------- *)
    
    let rec type_of_pretype ty = 
        match ty with
        | Stv n -> 
            stvs_translated := true
            let s = "?" ^ (string_of_int n)
            mk_vartype(s)
        | Utv(v) -> mk_vartype(v)
        | Ptycon(con, args) -> mk_type(con, map type_of_pretype args)
    
    (* ----------------------------------------------------------------------- *)
    (* Maps preterms to terms.                                                 *)
    (* ----------------------------------------------------------------------- *)
    
    let term_of_preterm = 
        let rec term_of_preterm ptm = 
            match ptm with
            | Varp(s, pty) -> mk_var(s, type_of_pretype pty)
            | Constp(s, pty) -> mk_mconst(s, type_of_pretype pty)
            | Combp(l, r) -> mk_comb(term_of_preterm l, term_of_preterm r)
            | Absp(v, bod) -> mk_gabs(term_of_preterm v, term_of_preterm bod)
            | Typing(ptm, pty) -> term_of_preterm ptm
        let report_type_invention() = 
            if !stvs_translated
            then 
                if !type_invention_error
                then failwith "typechecking error (cannot infer type of variables)"
                else warn !type_invention_warning "inventing type variables"
        fun ptm -> 
            stvs_translated := false
            let tm = term_of_preterm ptm
            report_type_invention()
            tm
    
    (* ----------------------------------------------------------------------- *)
    (* Overall typechecker: initial typecheck plus overload resolution pass.   *)
    (* ----------------------------------------------------------------------- *)
    
    let retypecheck venv ptm = 
        let ty = new_type_var()
        let ptm', _, env = 
            try 
                typify ty (ptm, venv, undefined)
            with
            | Failure e -> failwith("typechecking error (initial type assignment):" ^ e)
        let env' = 
            try 
                resolve_interface ptm' (fun e -> e) env
            with
            | Failure _ -> failwith "typechecking error (overload resolution)"
        let ptm'' = solve_preterm env' ptm'
        ptm''
    type_of_pretype, term_of_preterm, retypecheck