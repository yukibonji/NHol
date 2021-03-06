﻿(*

Copyright 1998 University of Cambridge
Copyright 1998-2007 John Harrison
Copyright 2013 Jack Pappas, Anh-Dung Phan, Eric Taucher, Domenico Masini

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

#if USE
open System
open System.Diagnostics

open FSharp.Compatibility.OCaml
open FSharp.Compatibility.OCaml.Num

open ExtCore.Control
open ExtCore.Control.Collections
#else
/// Various useful general library functions.
module NHol.lib

open System
open System.Diagnostics

open FSharp.Compatibility.OCaml
open FSharp.Compatibility.OCaml.Num

open ExtCore.Control
open ExtCore.Control.Collections

open NHol
open system

#endif

// Log module entry.
infof "Entering lib.fs"

//
type Protected<'T> = Choice<'T, exn>

// The exception fired by failwith is used as a control flow.
// KeyNotFoundException is not recognized in many cases, so we have to use redefine Failure for compatibility.
// Using exception as a control flow should be eliminated in the future.
let (|Failure|_|)(exn : exn) = 
    match exn with
    | :? System.Collections.Generic.KeyNotFoundException -> Some exn.Message
    | :? System.ArgumentException -> Some exn.Message
    | Microsoft.FSharp.Core.Operators.Failure s -> Some s
    | _ -> None

/// Creates a Failure exception with the specified message and the given exception
/// as the Failure's InnerException.
let inline nestedFailure innerException message : exn =
    exn (message, innerException)

/// Like failwith, but nests the specified exception within the failure exception.
let inline nestedFailwith innerException message =
    raise <| nestedFailure innerException message

(* ------------------------------------------------------------------------- *)
(* Some ExtCore-related functions used within NHol.                          *)
(* ------------------------------------------------------------------------- *)

/// Fail with empty string.
let inline fail () : 'T = failwith ""

// Follow the naming convention of ExtCore
[<RequireQualifiedAccess>]
module Choice =
    (* These functions are generally useful, and may be moved into ExtCore in the future. *)
    
    /// Composes two functions designed for use with the 'choice' workflow.
    /// This function is analagous to the F# (>>) operator.
    // NOTE : This function has been added to ExtCore for the 0.8.33 release.
    let compose (f : 'T -> Choice<'U, 'Error>) (g : 'U -> Choice<'V, 'Error>) =
        f >> (Choice.bind g)

    /// Composes two functions designed for use with the 'choice' workflow.
    /// This function is analagous to the F# (<<) operator.
    // NOTE : This function has been added to ExtCore for the 0.8.33 release.
    let composeBack (f : 'U -> Choice<'V, 'Error>) (g : 'T -> Choice<'U, 'Error>) =
        g >> (Choice.bind f)


    module List =
        /// <summary>Applies a function to each element of the collection, threading an accumulator argument
        /// through the computation. If the input function is <c>f</c> and the elements are <c>i0...iN</c> then computes 
        /// <c>f i0 (...(f iN-1 iN))</c>.
        /// </summary>
        /// <remarks>Raises <c>System.ArgumentException</c> if <c>list</c> is empty</remarks>
        /// <param name="reduction">The function to reduce two list elements to a single element.</param>
        /// <param name="list">The input list.</param>
        /// <exception cref="System.ArgumentException">Thrown when the list is empty.</exception>
        /// <returns>The final reduced value.</returns>
        // NOTE : This function has been added to ExtCore for the 0.8.33 release.
        [<CompiledName("ReduceBack")>]
        let reduceBack (reduction : 'T -> 'T -> Choice<'T, 'Error>) (list : 'T list) =
            // Preconditions
            checkNonNull "list" list

            // Extract the first element in the list then fold over the tail, using the first element
            // as the initial state value. If the list contains only one element, we return immediately.
            // NOTE : In order to reduce _backwards_ over the list, we reverse the list before calling fold.
            match List.rev list with
            | [] ->
                invalidArg "list" "The input list was empty."
            | [x] ->
                Choice1Of2 x
            | hd :: tl ->
                Choice.List.fold reduction hd tl

        /// Applies a destructor in right-associative mode a specified number of times.
        let rec nsplit dest clist x = 
            choice {
                if List.isEmpty clist then
                    return [], x
                else 
                    let! l, r = dest x
                    let! ll, y = nsplit dest (List.tail clist) r
                    return l :: ll, y
            }

    /// If BUGGY directive is defined, an exception could be raised
    /// Otherwise, we just ignore the choice value
    let inline ignoreOrRaise value : unit =
#if BUGGY
       ExtCore.Choice.bindOrRaise value
#else
        ignore value
#endif        

    (* These functions are specific to this project, and so probably won't be included in ExtCore. *)

    // Instead of using the Choice.get available in ExtCore, we redefine it here
    // as an alias for ExtCore.Choice.bindOrRaise, so we'll get more information when
    // an error occurs.
    let inline get (value : Choice<'T, exn>) =
        ExtCore.Choice.bindOrRaise value

    // The Choice.failwith in ExtCore returns the error string as-is, instead of wrapping it in an exception.
    // We could modify NHol to work the same way, we'd just need to use the Choice.bindOrFail function at the call sites.
    let inline failwith msg : Choice<'T, exn> =
        Choice2Of2 <| exn msg

    let inline nestedFailwith innerException message : Choice<'T, exn> =
        Choice2Of2 <| nestedFailure innerException message

    [<Obsolete("Uses of Choice.fail should be changed to Choice.failwith to provide better information when an error occurs.")>]
    let inline fail () : Choice<'T, exn> =
        failwith ""

    // NOTE : This is slightly different than the Choice.attempt from ExtCore --
    // this one only catches exceptions which match the Failure pattern.
    let attempt f : Choice<'T, exn> = 
        try
            Choice1Of2 <| f()
        with Failure _ as e ->
            Choice2Of2 e

    let attemptNested f : Choice<'T, exn> = 
        try
            f ()
        with Failure _ as e ->
            Choice2Of2 e

    let fill defaultResult (value : Choice<'T, 'Error>) =
        match value with
        | Choice1Of2 result ->
            result
        | Choice2Of2 _ ->
            defaultResult

    let getOrFailure2 msg (value : Choice<Choice<'T, exn> * Choice<'U, exn>, exn>) =
        match value with
        | Choice1Of2 (result1, result2) ->
            (result1, result2)
        | Choice2Of2 e ->
            (nestedFailwith e msg, nestedFailwith e msg) 

    let getOrFailure3 msg (value : Choice<Choice<'T, exn> * Choice<'U, exn> * Choice<'V, exn>, exn>) =
        match value with
        | Choice1Of2 (result1, result2, result3) ->
            (result1, result2, result3)
        | Choice2Of2 e ->
            (nestedFailwith e msg, nestedFailwith e msg, nestedFailwith e msg) 

    /// Applies the specified binding function to a choice value representing an error value
    /// (Choice2Of2). If the choice value represents a result value (Choice1Of2), the result value
    /// is passed through without modification.
    let bindError (binding : 'Error -> Choice<'T, _>) value =
        match value with
        | Choice1Of2 result ->
            Choice1Of2 result
        | Choice2Of2 error ->
            // NOTE: until we normalize all uses of bindError, we will categorize error patterns here
            match error with
            | Failure _ ->
                binding error
            | _ -> Choice2Of2 error

    /// Iterates a function a fixed number of times.
    /// The iteration will be short-circuited if/when the function returns an error.
    let rec funpow n f x : Choice<'T, 'Error> =
        choice {
        if n < 1 then
            return x
        else
            let! next_x = f x
            return! funpow (n - 1) f next_x
        }


(* ------------------------------------------------------------------------- *)
(* Functions needed for OCaml compatibility. These augment or supercede      *)
(* the functionality of the FSharp.Compatibility.OCaml library. Some of      *)
(* these may be included in a future release of FSharp.Compatibility.OCaml.  *)
(* ------------------------------------------------------------------------- *)

module Ratio =
    open System.Diagnostics
    open FSharp.Compatibility.OCaml
    open FSharp.Compatibility.OCaml.Num

    let inline numerator_ratio(r : Ratio.ratio) = r.Numerator
    let inline denominator_ratio(r : Ratio.ratio) = r.Denominator

    //
    let [<Literal>] private normalize_ratio_warning =
        "Ratio.normalize_ratio does not actually normalize the value \
         (the functionality has not yet been implemented), so other functions \
         which rely on that invariant will not work properly."

    // NOTE : not sure what kind of normalization should be done here
    // TODO : Implement this function correctly in the next version of
    // FSharp.Compatibility.OCaml, then upgrade to that version ASAP.
    [<Experimental(normalize_ratio_warning)>]
    let normalize_ratio x =
        Debug.Write "Warning: "
        Debug.WriteLine normalize_ratio_warning
        x


(* ------------------------------------------------------------------------- *)
(* NHol-specific helper functions which don't belong in ExtCore or           *)
(* FSharp.Compatibility.OCaml.                                               *)
(* ------------------------------------------------------------------------- *)

module Option =
    /// Gets the value associated with an option; if the option is None,
    /// fails with the specified error message.
    let getOrFailWith msg (value : 'T option) =
        match value with
        | Some x -> x
        | None -> failwith msg

    let toChoiceWithError msg (value : 'T option) =
        match value with
        | Some v -> Choice.result v
        | None -> Choice.failwith msg


(* ------------------------------------------------------------------------- *)
(* Combinators.                                                              *)
(* ------------------------------------------------------------------------- *)

let inline curry f x y = f(x, y)
let inline uncurry f (x, y) = f x y
/// Identity function.
[<Obsolete("Use the 'id' function instead.")>]
let inline I x = x
/// Returns the first of the two applied arguments.
let inline K x y = x
// TODO : Replace with 'flip' from ExtCore.
/// Given a function taking two arguments, returns a new function which calls
/// the original function but swaps the order in which the arguments are applied.
let inline C f x y = f y x
/// Applies the argument to the function twice.
let inline W f x = f x x
let (||>>) = fun f g (x, y) -> (f x, g y)

(* ------------------------------------------------------------------------- *)
(* List basics.                                                              *)
(* ------------------------------------------------------------------------- *)

/// Computes the first element (the head) of a list.
// OPTIMIZE : Make this an alias for List.head.
let hd l =
    match l with
    | h :: t -> h
    | _ -> failwith "hd"

/// Computes the tail of a list (the original list less the first element).
// OPTIMIZE : Make this an alias for List.tail.
let tl l = 
    match l with
    | h :: t -> t
    | _ -> failwith "tl"

/// Applies a function to every element of a list.
// OPTIMIZE : Make this an alias for List.map.
let map f = 
    let rec mapf l = 
        match l with
        | [] -> []
        | (x :: t) -> 
            let y = f x
            y :: (mapf t)
    mapf

/// Computes the last element of a list.
// OPTIMIZE : Make this an alias for List.last (from ExtCore).
let rec last l = 
    match l with
    | [x] -> x
    | (h :: t) -> last t
    | [] -> failwith "last"

/// Computes the sub-list of a list consisting of all but the last element.
// OPTIMIZE : Make this an alias for List.dropLast (from ExtCore).
let rec butlast l = 
    match l with
    | [_] -> []
    | (h :: t) -> h :: (butlast t)
    | [] -> failwith "butlast"

/// <summary>
/// Removes the last element from a list, returning both
/// the last element and the remainder of the list.
/// <c>extractLast lst</c> is equivalent to <c>(last lst), (butlast lst)</c>.
/// </summary>
let extractLast lst =
    match lst with
    | [] ->
        invalidArg "lst" "The input list is empty."
    | [x] ->
        x, []
    | _ ->
        let rev_lst = List.rev lst
        List.head rev_lst, List.rev (List.tail rev_lst)

/// Extracts a specified element from a list.
// OPTIMIZE : Make this an alias for List.nth.
let rec el n l = 
    if n = 0 then hd l
    else el (n - 1) (tl l)

/// Reverses a list.
// OPTIMIZE : Make this an alias for List.rev.
let rev = 
    let rec rev_append acc l = 
        match l with
        | [] -> acc
        | h :: t -> rev_append (h :: acc) t
    fun l -> rev_append [] l

/// Maps a binary function over two lists to create one new list.
// OPTIMIZE : Make this an alias for List.map2.
let rec map2 f l1 l2 = 
    match (l1, l2) with
    | [], [] -> []
    | (h1 :: t1), (h2 :: t2) -> 
        let h = f h1 h2
        h :: (map2 f t1 t2)
    | _ -> failwith "map2: length mismatch"

(* ------------------------------------------------------------------------- *)
(* Attempting function or predicate applications.                            *)
(* ------------------------------------------------------------------------- *)

/// Checks that a value satisfies a predicate.
let check p x : Protected<_> = 
    if p x then Choice.result x
    else Choice.failwith "check"

(* ------------------------------------------------------------------------- *)
(* Repetition of a function.                                                 *)
(* ------------------------------------------------------------------------- *)

/// Iterates a function a fixed number of times.
[<Obsolete("Use Choice.funpow instead.")>]
let rec funpow n f x = 
    if n < 1 then x
    else funpow (n - 1) f (f x)

/// Repeatedly apply a function until it fails.
let rec repeat f x = 
    match f x with
    | Some y -> 
        repeat f y
    | None -> x

(* ------------------------------------------------------------------------- *)
(* To avoid consing in various situations, we propagate this exception.      *)
(* I should probably eliminate this and use pointer EQ tests instead.        *)
(* ------------------------------------------------------------------------- *)

//exception Unchanged

(* ------------------------------------------------------------------------- *)
(* Various versions of list iteration.                                       *)
(* ------------------------------------------------------------------------- *)

/// List iteration function. Applies a binary function between adjacent elements of a list.
// OPTIMIZE : Make this an alias for List.foldBack.
let rec itlist f l b = 
    match l with
    | [] -> b
    | (h :: t) -> f h (itlist f t b)

/// Applies a binary function between adjacent elements of the reverse of a list.
// OPTIMIZE : Make this an alias for List.fold.
let rec rev_itlist f l b = 
    match l with
    | [] -> b
    | (h :: t) -> rev_itlist f t (f h b)

/// List iteration function. Applies a binary function between adjacent elements of a list.
// OPTIMIZE : Make this an alias for List.reduceBack.
let rec end_itlist f l = 
    match l with
    | [] -> failwith "end_itlist"
    | [x] -> x
    | (h :: t) -> f h (end_itlist f t)

/// Applies a paired function between adjacent elements of 2 lists.
// OPTIMIZE : Make this an alias for List.foldBack2.
let rec itlist2 f l1 l2 b = 
    match (l1, l2) with
    | ([], []) -> b
    | (h1 :: t1, h2 :: t2) -> f h1 h2 (itlist2 f t1 t2 b)
    | _ -> failwith "itlist2"

/// Applies a paired function between adjacent elements of 2 lists.
// OPTIMIZE : Make this an alias for List.fold2.
let rec rev_itlist2 f l1 l2 b = 
    match (l1, l2) with
    | ([], []) -> b
    | (h1 :: t1, h2 :: t2) -> rev_itlist2 f t1 t2 (f h1 h2 b)
    | _ -> failwith "rev_itlist2"

(* ------------------------------------------------------------------------- *)
(* Iterative splitting (list) and stripping (tree) via destructor.           *)
(* ------------------------------------------------------------------------- *)

/// Applies a binary destructor repeatedly in left-associative mode.
// OPTIMIZE : Anywhere this function is called, and the state is discarded from the result,
// we can immediately change to using List.unfold from ExtCore. To handle other call sites,
// we could copy in the code for List.unfold from ExtCore and modify it to also return the
// final state value, in which case it'd give identical results to this function.
let rec splitlist dest x = 
    match dest x with
    | Some (l, r) ->
        let ls, res = splitlist dest r
        (l :: ls, res)
    | None -> ([], x)

/// Applies a binary destructor repeatedly in right-associative mode.
// OPTIMIZE : Anywhere this function is called, and the state is discarded from the result,
// we can immediately change to using List.unfoldBack from ExtCore. To handle other call sites,
// we could copy in the code for List.unfoldBack from ExtCore and modify it to also return the
// final state value, in which case it'd give identical results to this function.
let rev_splitlist dest = 
    let rec rsplist ls x = 
        match dest x with
        | Some (l, r) ->
            rsplist (r :: ls) l
        | None -> (x, ls)
    fun x -> rsplist [] x

/// Applies a binary destructor repeatedly, flattening the construction tree into a list.
let striplist dest = 
    let rec strip x acc = 
        match dest x with
        | Some (l, r) ->
            strip l (strip r acc)
        | None -> x :: acc
    fun x -> strip x []

(* ------------------------------------------------------------------------- *)
(* Apply a destructor as many times as elements in list.                     *)
(* ------------------------------------------------------------------------- *)

/// Applies a destructor in right-associative mode a specified number of times.
// OPTIMIZE : It seems like this could be simplified by using one of the State.List functions from ExtCore.
let rec nsplit dest clist x = 
    if List.isEmpty clist then
        [], x
    else 
        let l, r = dest x
        let ll, y = nsplit dest (tl clist) r
        l :: ll, y

(* ------------------------------------------------------------------------- *)
(* Replication and sequences.                                                *)
(* ------------------------------------------------------------------------- *)

/// Makes a list consisting of a value replicated a specified number of times.
// OPTIMIZE : Make this an alias for List.replicate.
let rec replicate x n = 
    if n < 1 then []
    else x :: (replicate x (n - 1))

/// Gives a finite list of integers between the given bounds.
// OPTIMIZE : Make this an alias for [m..n]
let rec (--) m n =
    if m > n then []
    else m :: ((m + 1) -- n)

(* ------------------------------------------------------------------------- *)
(* Various useful list operations.                                           *)
(* ------------------------------------------------------------------------- *)

/// Tests a list to see if all its elements satisfy a predicate.
// OPTIMIZE : Make this an alias for List.forall.
let rec forall p l = 
    match l with
    | [] -> true
    | h :: t -> p(h) && forall p t

/// Tests if corresponding elements of two lists all satisfy a relation.
// OPTIMIZE : Make this an alias for List.forall2.
let rec forall2 p l1 l2 = 
    match (l1, l2) with
    | [], [] -> true
    | (h1 :: t1, h2 :: t2) -> p h1 h2 && forall2 p t1 t2
    | _ -> false

/// Tests a list to see if some element satisfy a predicate.
// OPTIMIZE : Make this an alias for List.exists.
let rec exists p l = 
    match l with
    | [] -> false
    | h :: t -> p(h) || exists p t

/// Computes the length of a list.
// OPTIMIZE : Make this an alias for List.length.
let length = 
    let rec len k l = 
        if l = [] then k
        else len (k + 1) (tl l)
    fun l -> len 0 l

/// Filters a list to the sublist of elements satisfying a predicate.
// OPTIMIZE : Make this an alias for List.filter.
let rec filter p l = 
    match l with
    | [] -> l
    | h :: t -> 
        let t' = filter p t
        if p(h) then 
            if t' == t then l
            else h :: t'
        else t'

/// Separates a list into two lists using a predicate.
// OPTIMIZE : Make this an alias for List.partition.
let rec partition p l = 
    match l with
    | [] -> [], l
    | h :: t -> 
        let yes, no = partition p t
        if p(h) then 
            (if yes == t then l, []
             else h :: yes, no)
        else 
            (if no == t then [], l
             else yes, h :: no)

/// Applies a function to every element of a list, returning a list of results for those elements for which application succeeds.
[<Obsolete("Use 'List.choose' instead.")>]
let inline mapfilter f l =
    List.choose f l

/// Returns the first element of a list which satisfies a predicate.
[<Obsolete("Use 'List.tryFind' instead.")>]
let inline find p l =
    List.tryFind p l

/// Returns the result of the first successful application of a function to the elements of a list.
[<Obsolete("Use 'List.tryPick' instead.")>]
let inline tryfind f l =
    List.tryPick f l

/// Flattens a list of lists into one long list.
// OPTIMIZE : Make this an alias for List.concat.
let flat l =
    itlist (@) l []

/// Separates the first element of a list to satisfy a predicate from the rest of the list.
// OPTIMIZE : Rewrite this function using ListZipper from ExtCore.
let rec remove p l = 
    match l with
    | [] -> None
    | (h :: t) -> 
        if p(h) then Some (h, t)
        else 
            remove p t
            |> Option.map (fun (y, n) -> (y, h :: n))

/// Chops a list into two parts at a specified point.
[<Obsolete("Use 'List.take' instead.")>]
let inline chop_list n l = 
    List.take n l

/// Returns position of given element in list.
// OPTIMIZE : Make this an alias for List.findIndex.
// Or, for more safety, modify this to return an option value, fix the call sites,
// then make this an alias for List.tryFindIndex.
let index x = 
    let rec ind n l = 
        match l with
        | [] -> failwith "index"
        | (h :: t) -> 
            if compare x h = 0 then n
            else ind (n + 1) t
    ind 0

(* ------------------------------------------------------------------------- *)
(* "Set" operations on lists.                                                *)
(* ------------------------------------------------------------------------- *)

/// Tests whether a list contains a certain member.
let rec mem x lis = 
    match lis with
    | [] -> false
    | (h :: t) -> compare x h = 0 || mem x t

/// Adds element to the head of a list if not already present.
let insert x l = 
    if mem x l then l
    else x :: l

/// Computes the union of two 'sets'.
let union l1 l2 = itlist insert l1 l2

/// Performs the union of a set of sets.
let unions l = itlist union l []

/// Computes the intersection of two 'sets'.
let intersect l1 l2 = filter (fun x -> mem x l2) l1

/// Computes the set-theoretic difference of two 'sets'.
let subtract l1 l2 = filter (fun x -> not(mem x l2)) l1

/// Tests if one list is a subset of another.
let subset l1 l2 = forall (fun t -> mem t l2) l1

/// Tests two 'sets' for equality.
let set_eq l1 l2 = subset l1 l2 && subset l2 l1

(* ------------------------------------------------------------------------- *)
(* Association lists.                                                        *)
(* ------------------------------------------------------------------------- *)

/// Searches a list of pairs for a pair whose first component equals a specified value.
let rec assoc a l = 
    match l with
    | [] -> None
    | (x, y) :: t -> 
        if compare x a = 0 then Some y
        else assoc a t

/// Searches a list of pairs for a pair whose second component equals a specified value.
let rec rev_assoc a l =
    match l with
    | [] -> None
    | (x, y) :: t ->
        if compare y a = 0 then Some x
        else rev_assoc a t

(* ------------------------------------------------------------------------- *)
(* Zipping, unzipping etc.                                                   *)
(* ------------------------------------------------------------------------- *)

/// Converts a list of pairs into a pair of lists.
// OPTIMIZE : Make this an alias for List.zip.
let rec zip l1 l2 = 
    match (l1, l2) with
    | ([], []) -> []
    | (h1 :: t1, h2 :: t2) -> (h1, h2) :: (zip t1 t2)
    | _ -> failwith "zip"

/// Converts a list of pairs into a pair of lists.
// OPTIMIZE : Make this an alias for List.unzip.
let rec unzip = function 
    | [] ->
        [], []
    | (a, b) :: rest ->
        let alist, blist = unzip rest
        (a :: alist, b :: blist)

(* ------------------------------------------------------------------------- *)
(* Sharing out a list according to pattern in list-of-lists.                 *)
(* ------------------------------------------------------------------------- *)

/// Shares out the elements of the second list according to pattern in first.
let rec shareout pat all =
    if pat = [] then []
    else 
        let l, r = List.take (length(hd pat)) all
        l :: (shareout (tl pat) r)

(* ------------------------------------------------------------------------- *)
(* Iterating functions over lists.                                           *)
(* ------------------------------------------------------------------------- *)

/// Apply imperative function to each element of a list.
// OPTIMIZE : Make this an alias for List.iter.
let rec do_list f l = 
    match l with
    | [] -> ()
    | h :: t -> 
        f h
        do_list f t

(* ------------------------------------------------------------------------- *)
(* Sorting.                                                                  *)
(* ------------------------------------------------------------------------- *)

/// Sorts a list using a given transitive 'ordering' relation.
// OPTIMIZE : Make this an alias for List.sortWith.
let rec sort cmp lis = 
    match lis with
    | [] -> []
    | piv :: rest -> 
        let r, l = partition (cmp piv) rest
        (sort cmp l) @ (piv :: (sort cmp r))

(* ------------------------------------------------------------------------- *)
(* Removing adjacent (NB!) equal elements from list.                         *)
(* ------------------------------------------------------------------------- *)

/// Eliminate adjacent identical elements from a list.
let rec uniq l = 
    match l with
    | x :: (y :: _ as t) -> 
        let t' = uniq t
        if compare x y = 0 then t'
        elif t' == t then l
        else x :: t'
    | _ -> l

(* ------------------------------------------------------------------------- *)
(* Convert list into set by eliminating duplicates.                          *)
(* ------------------------------------------------------------------------- *)

/// Removes repeated elements from a list. Makes a list into a 'set'.
let setify s = uniq(sort (fun x y -> compare x y <= 0) s)

(* ------------------------------------------------------------------------- *)
(* String operations (surely there is a better way...)                       *)
(* ------------------------------------------------------------------------- *)

/// Concatenates a list of strings into one string.
let implode (l : string list) : string =
    // Preconditions
    checkNonNull "l" l

    // Pattern-match on the list to provide optimized implementations
    // for lists with just a few elements.
    match l with
    | [] -> ""
    | [x] -> x
    | x :: [y] ->
        System.String.Concat (x, y)
    | x :: y :: [z] ->
        System.String.Concat (x, y, z)
    | w :: x :: y :: [z] ->
        System.String.Concat (w, x, y, z)
    | _ ->
        // TODO : If it's not much slower, just use System.String.Join here instead to simplify things.
        // System.String.Join ("", l)
        let sb = System.Text.StringBuilder ()
        let rec implode = function
            | [] ->
                sb.ToString ()
            | (hd : string) :: tl ->
                Debug.Assert (not <| isNull hd,
                    "The string list contains at least one element which is a null string.")
                sb.Append hd |> ignore
                implode tl
        implode l

/// Converts a string into a list of single-character strings.
let explode (s : string) : string list =
    // Preconditions
    checkNonNull "s" s

    let rec exap n l = 
        if n < 0 then l
        else exap (n - 1) ((s.[n].ToString()) :: l)
    exap (String.length s - 1) []

(* ------------------------------------------------------------------------- *)
(* Greatest common divisor.                                                  *)
(* ------------------------------------------------------------------------- *)

/// Computes greatest common divisor of two integers.
let gcd = 
    let rec gxd x y = 
        if y = 0 then x
        else gxd y (x % y)
    fun x y -> 
        let x' = abs x
        let y' = abs y
        if x' < y' then gxd y' x'
        else gxd x' y'

(* ------------------------------------------------------------------------- *)
(* Some useful functions on "num" type.                                      *)
(* ------------------------------------------------------------------------- *)

let num_0 = Int 0
let num_1 = Int 1
let num_2 = Int 2
let num_10 = Int 10

let pow2 (n:int) = 
    if n < 0 then
        let n' = System.Math.Abs(n)
        (Int 1) / (power_num num_2 (Int n'))
    else power_num num_2 (Int n)

let pow10 (n:int) =
    if n < 0 then 
        let n' = System.Math.Abs(n)
        (Int 1) / (power_num num_10 (Int n'))
    else power_num num_10 (Int n)

/// Returns numerator and denominator of normalized fraction.
let numdom r =
    let r' = Ratio.normalize_ratio(ratio_of_num r)
    num_of_big_int(Ratio.numerator_ratio r'), num_of_big_int(Ratio.denominator_ratio r')

/// Returns numerator of rational number in canonical form.
let numerator = fst << numdom
/// Returns denominator of rational number in canonical form.
let denominator = snd << numdom

module Big_int =
    let inline gcd_big_int (a : bigint) (b : bigint) : bigint =
        bigint.GreatestCommonDivisor(a,b) 

    let big_int_of_ratio r =
        let numerator, denominator = numdom r
        if denominator = num_of_big_int System.Numerics.BigInteger.One then
            match numerator with
            | Int i -> bigint i
            | Big_int i -> i
            | Ratio _ ->
                failwith "big_int_of_ratio"
        else failwith "big_int_of_ratio"

module Num =
    let big_int_of_num (n : num) : bigint =
        match n with
        | Int i ->
            bigint i
        | Big_int i ->
            i
        | Ratio q ->
            Big_int.big_int_of_ratio n

/// Computes greatest common divisor of two unlimited-precision integers.
let gcd_num n1 n2 = num_of_big_int(Big_int.gcd_big_int (Num.big_int_of_num n1) (Num.big_int_of_num n2))

/// Computes lowest common multiple of two unlimited-precision integers.
let lcm_num x y = 
    if x =/ num_0 && y =/ num_0 then num_0
    else abs_num((x */ y) / gcd_num x y)

(* ------------------------------------------------------------------------- *)
(* All pairs arising from applying a function over two lists.                *)
(* ------------------------------------------------------------------------- *)

/// Compute list of all results from applying function to pairs from two lists.
let rec allpairs f l1 l2 = 
    match l1 with
    | h1 :: t1 -> itlist (fun x a -> f h1 x :: a) l2 (allpairs f t1 l2)
    | [] -> []

(* ------------------------------------------------------------------------- *)
(* Issue a report with a newline.                                            *)
(* ------------------------------------------------------------------------- *)

/// Prints a string and a following line break.
let report s =
    Format.print_string s
    Format.print_newline()

(* ------------------------------------------------------------------------- *)
(* Convenient function for issuing a warning.                                *)
(* ------------------------------------------------------------------------- *)

/// Prints out a warning string.
let warn cond s =
    if cond then
        report("Warning: " + s)
    
        // Also log the warning with NLog.
//        logger.Warn s

(* ------------------------------------------------------------------------- *)
(* Flags to switch on verbose mode.                                          *)
(* ------------------------------------------------------------------------- *)

/// Flag to control verbosity of informative output.
let verbose = ref true
/// Flag to determine whether 'time' function outputs CPU time measure.
let report_timing = ref true

(* ------------------------------------------------------------------------- *)
(* Switchable version of "report".                                           *)
(* ------------------------------------------------------------------------- *)

/// Output a string and newline if and only if 'verbose' flag is set.
let remark s =
    if !verbose then report s
    
    // Also log the message with NLog, regardless of whether the 'verbose' flag is set.
//    logger.Info s

(* ------------------------------------------------------------------------- *)
(* Time a function.                                                          *)
(* ------------------------------------------------------------------------- *)

/// Report CPU time taken by a function.
let time f x = 
    if not(!report_timing) then f x
    else 
        let start_time = Sys.time()
        try 
            let result = f x
            let finish_time = Sys.time()
            report("CPU time (user): " + (string_of_float(finish_time -. start_time)))
            result
        with _ -> 
            let finish_time = Sys.time()
            Format.print_string
                ("Failed after (user) CPU time of " + (string_of_float(finish_time -. start_time)) + ": ")
            reraise ()

(* ------------------------------------------------------------------------- *)
(* Versions of assoc and rev_assoc with default rather than failure.         *)
(* ------------------------------------------------------------------------- *)

/// Looks up item in association list taking default in case of failure.
let rec assocd a l d = 
    match l with
    | [] -> d
    | (x, y) :: t -> 
        if compare x a = 0 then y
        else assocd a t d

/// Looks up item in association list taking default in case of failure.
let rec rev_assocd a l d = 
    match l with
    | [] -> d
    | (x, y) :: t -> 
        if compare y a = 0 then x
        else rev_assocd a t d

(* ------------------------------------------------------------------------- *)
(* Version of map that avoids rebuilding unchanged subterms.                 *)
(* ------------------------------------------------------------------------- *)

/// Maps a function of type 'a -> 'a over a list, optimizing the unchanged case.
let rec qmap f l = 
    match l with
    | h :: t -> 
        let h' = f h
        let t' = qmap f t
        if h' == h && t' == t then l
        else h' :: t'
    | _ -> l

(* ------------------------------------------------------------------------- *)
(* Merging and bottom-up mergesort.                                          *)
(* ------------------------------------------------------------------------- *)

/// Merges together two sorted lists with respect to a given ordering.
// OPTIMIZE : Make this tail-recursive (just use an accumulator and call List.rev at the end).
let rec merge ord l1 l2 = 
    match l1 with
    | [] -> l2
    | h1 :: t1 -> 
        match l2 with
        | [] -> l1
        | h2 :: t2 -> 
            if ord h1 h2 then h1 :: (merge ord t1 l2)
            else h2 :: (merge ord l1 t2)

/// Sorts the list with respect to given ordering using mergesort algorithm.
let mergesort ord = 
    let rec mergepairs l1 l2 = 
        match (l1, l2) with
        | ([s], []) -> s
        | (l, []) -> mergepairs [] l
        | (l, [s1]) -> mergepairs (s1 :: l) []
        | (l, (s1 :: s2 :: ss)) -> mergepairs ((merge ord s1 s2) :: l) ss
    fun l -> 
        if l = [] then []
        else mergepairs [] (map (fun x -> [x]) l)

(* ------------------------------------------------------------------------- *)
(* Common measure predicates to use with "sort".                             *)
(* ------------------------------------------------------------------------- *)

/// Returns a total ordering based on a measure function.
let increasing f x y = compare (f x) (f y) < 0
/// When applied to a measure function f, the call increasing f returns a binary function
/// ordering elements in a call increasing f x y by f(y) <? f(x), where the ordering <? is
/// the OCaml polymorphic ordering.
let decreasing f x y = compare (f x) (f y) > 0

(* ------------------------------------------------------------------------- *)
(* Polymorphic finite partial functions via Patricia trees.                  *)
(*                                                                           *)
(* The point of this strange representation is that it is canonical (equal   *)
(* functions have the same encoding) yet reasonably efficient on average.    *)
(*                                                                           *)
(* Idea due to Diego Olivier Fernandez Pons (OCaml list, 2003/11/10).        *)
(* ------------------------------------------------------------------------- *)

// OPTIMIZE : Replace with IntMap type from ExtCore.
type func<'a, 'b> = 
    | Empty
    | Leaf of int * ('a * 'b) list
    | Branch of int * int * func<'a, 'b> * func<'a, 'b>

(* ------------------------------------------------------------------------- *)
(* Undefined function.                                                       *)
(* ------------------------------------------------------------------------- *)

/// Completely undefined finite partial function.
let undefined = Empty

(* ------------------------------------------------------------------------- *)
(* In case of equality comparison worries, better use this.                  *)
(* ------------------------------------------------------------------------- *)

/// Tests if a finite partial function is defined nowhere.
// OPTIMIZE : Replace with IntMap.isEmpty from ExtCore (once func<_,_> is replaced with IntMap).
let is_undefined f = 
    match f with
    | Empty -> true
    | _ -> false

(* ------------------------------------------------------------------------- *)
(* Operation analagous to "map" for lists.                                   *)
(* ------------------------------------------------------------------------- *)

/// Maps a function over the range of a finite partial function.
let mapf = 
    let rec map_list f l = 
        match l with
        | [] -> []
        | (x, y) :: t -> (x, f(y)) :: (map_list f t)
    let rec mapf f t = 
        match t with
        | Empty -> Empty
        | Leaf(h, l) -> Leaf(h, map_list f l)
        | Branch(p, b, l, r) -> Branch(p, b, mapf f l, mapf f r)
    mapf

(* ------------------------------------------------------------------------- *)
(* Operations analogous to "fold" for lists.                                 *)
(* ------------------------------------------------------------------------- *)

/// Folds an operation iteratively over the graph of a finite partial function.
let foldl = 
    let rec foldl_list f a l = 
        match l with
        | [] -> a
        | (x, y) :: t -> foldl_list f (f a x y) t
    let rec foldl f a t = 
        match t with
        | Empty -> a
        | Leaf(h, l) -> foldl_list f a l
        | Branch(p, b, l, r) -> foldl f (foldl f a l) r
    foldl

/// Folds an operation iteratively over the graph of a finite partial function.
let foldr = 
    let rec foldr_list f l a = 
        match l with
        | [] -> a
        | (x, y) :: t -> f x y (foldr_list f t a)
    let rec foldr f t a = 
        match t with
        | Empty -> a
        | Leaf(h, l) -> foldr_list f l a
        | Branch(p, b, l, r) -> foldr f l (foldr f r a)
    foldr

(* ------------------------------------------------------------------------- *)
(* Mapping to sorted-list representation of the graph, domain and range.     *)
(* ------------------------------------------------------------------------- *)

/// Returns the graph of a finite partial function.
let graph f = setify(foldl (fun a x y -> (x, y) :: a) [] f)
/// Returns domain of a finite partial function.
let dom f = setify(foldl (fun a x y -> x :: a) [] f)
/// Returns the domain of such a function, i.e. the set of result values for the points on which it is defined.
let ran f = setify(foldl (fun a x y -> y :: a) [] f)

(* ------------------------------------------------------------------------- *)
(* Application.                                                              *)
(* ------------------------------------------------------------------------- *)

/// Applies a finite partial function, with a backup function for undefined points.
let applyd = 
    let rec apply_listd l d x = 
        match l with
        | (a, b) :: t -> 
            let c = compare x a
            if c = 0 then Some b
            elif c > 0 then apply_listd t d x
            else d x
        | [] -> d x
    fun f d x -> 
        let k = Hashtbl.hash x
        let rec look t = 
            match t with
            | Leaf(h, l) when h = k -> apply_listd l d x
            | Branch(p, b, l, r) when (k ^^^ p) &&& (b - 1) = 0 -> 
                look(if k &&& b = 0 then l
                     else r)
            | _ -> d x
        look f

/// Applies a finite partial function, failing on undefined points.
let apply f = applyd f (fun x -> None)

/// Applies a finite partial function, with a default for undefined points.
let tryapplyd f a d = 
    // NOTE: this will not fail because there is no case returning None
    Option.get <| applyd f (fun x -> Some d) a

/// Tests if a finite partial function is defined on a certain domain value.
let defined f x = 
    Option.isSome <| apply f x

(* ------------------------------------------------------------------------- *)
(* Undefinition.                                                             *)
(* ------------------------------------------------------------------------- *)

/// Remove definition of a finite partial function on specific domain value.
let undefine = 
    let rec undefine_list x l = 
        match l with
        | (a, b as ab) :: t -> 
            let c = compare x a
            if c = 0 then t
            elif c < 0 then l
            else 
                let t' = undefine_list x t
                if t' == t then l
                else ab :: t'
        | [] -> []
    fun x -> 
        let k = Hashtbl.hash x
        let rec und t = 
            match t with
            | Leaf(h, l) when h = k -> 
                let l' = undefine_list x l
                if l' == l then t
                elif l' = [] then Empty
                else Leaf(h, l')
            | Branch(p, b, l, r) when k &&& (b - 1) = p -> 
                if k &&& b = 0 then 
                    let l' = und l
                    if l' == l then t
                    else 
                        (match l' with
                         | Empty -> r
                         | _ -> Branch(p, b, l', r))
                else 
                    let r' = und r
                    if r' == r then t
                    else 
                        (match r' with
                         | Empty -> l
                         | _ -> Branch(p, b, l, r'))
            | _ -> t
        und

(* ------------------------------------------------------------------------- *)
(* Redefinition and combination.                                             *)
(* ------------------------------------------------------------------------- *)

// (|->): Modify a finite partial function at one point.
// combine: Combine together two finite partial functions using pointwise operation.
let (|->), combine = 
    let newbranch p1 t1 p2 t2 = 
        let zp = p1 ^^^ p2
        let b = zp &&& (-zp)
        let p = p1 &&& (b - 1)
        if p1 &&& b = 0 then Branch(p, b, t1, t2)
        else Branch(p, b, t2, t1)
    let rec define_list (x, y as xy) l = 
        match l with
        | (a, b as ab) :: t -> 
            let c = compare x a
            if c = 0 then xy :: t
            elif c < 0 then xy :: l
            else ab :: (define_list xy t)
        | [] -> [xy]
    and combine_list op z l1 l2 = 
        match (l1, l2) with
        | [], _ -> l2
        | _, [] -> l1
        | ((x1, y1 as xy1) :: t1, (x2, y2 as xy2) :: t2) -> 
            let c = compare x1 x2
            if c < 0 then xy1 :: (combine_list op z t1 l2)
            elif c > 0 then xy2 :: (combine_list op z l1 t2)
            else 
                let y = op y1 y2
                let l = combine_list op z t1 t2
                if z(y) then l
                else (x1, y) :: l
    let (|->) x y = 
        let k = Hashtbl.hash x
        let rec upd t = 
            match t with
            | Empty -> Leaf(k, [x, y])
            | Leaf(h, l) -> 
                if h = k then Leaf(h, define_list (x, y) l)
                else newbranch h t k (Leaf(k, [x, y]))
            | Branch(p, b, l, r) -> 
                if k &&& (b - 1) <> p then newbranch p t k (Leaf(k, [x, y]))
                elif k &&& b = 0 then Branch(p, b, upd l, r)
                else Branch(p, b, l, upd r)
        upd
    let rec combine op z t1 t2 = 
        match (t1, t2) with
        | Empty, _ -> t2
        | _, Empty -> t1
        | Leaf(h1, l1), Leaf(h2, l2) -> 
            if h1 = h2 then 
                let l = combine_list op z l1 l2
                if l = [] then Empty
                else Leaf(h1, l)
            else newbranch h1 t1 h2 t2
        | (Leaf(k, lis) as lf), (Branch(p, b, l, r) as br) -> 
            if k &&& (b - 1) = p then 
                if k &&& b = 0 then 
                    (match combine op z lf l with
                     | Empty -> r
                     | l' -> Branch(p, b, l', r))
                else 
                    (match combine op z lf r with
                     | Empty -> l
                     | r' -> Branch(p, b, l, r'))
            else newbranch k lf p br
        | (Branch(p, b, l, r) as br), (Leaf(k, lis) as lf) -> 
            if k &&& (b - 1) = p then 
                if k &&& b = 0 then 
                    (match combine op z l lf with
                     | Empty -> r
                     | l' -> Branch(p, b, l', r))
                else 
                    (match combine op z r lf with
                     | Empty -> l
                     | r' -> Branch(p, b, l, r'))
            else newbranch p br k lf
        | Branch(p1, b1, l1, r1), Branch(p2, b2, l2, r2) -> 
            if b1 < b2 then 
                if p2 &&& (b1 - 1) <> p1 then newbranch p1 t1 p2 t2
                elif p2 &&& b1 = 0 then 
                    (match combine op z l1 t2 with
                     | Empty -> r1
                     | l -> Branch(p1, b1, l, r1))
                else 
                    (match combine op z r1 t2 with
                     | Empty -> l1
                     | r -> Branch(p1, b1, l1, r))
            elif b2 < b1 then 
                if p1 &&& (b2 - 1) <> p2 then newbranch p1 t1 p2 t2
                elif p1 &&& b2 = 0 then 
                    (match combine op z t1 l2 with
                     | Empty -> r2
                     | l -> Branch(p2, b2, l, r2))
                else 
                    (match combine op z t1 r2 with
                     | Empty -> l2
                     | r -> Branch(p2, b2, l2, r))
            elif p1 = p2 then 
                (match (combine op z l1 l2, combine op z r1 r2) with
                 | (Empty, r) -> r
                 | (l, Empty) -> l
                 | (l, r) -> Branch(p1, b1, l, r))
            else newbranch p1 t1 p2 t2
    (|->), combine

(* ------------------------------------------------------------------------- *)
(* Special case of point function.                                           *)
(* ------------------------------------------------------------------------- *)

/// Gives a one-point finite partial function.
let (|=>) = fun x y -> (x |-> y) undefined

(* ------------------------------------------------------------------------- *)
(* Grab an arbitrary element.                                                *)
(* ------------------------------------------------------------------------- *)

/// Picks an arbitrary element from the graph of a finite partial function.
let rec choose t =
    match t with
    | Empty ->
        Choice.failwith "choose: completely undefined function"
    | Leaf(h, l) ->
        Choice.result <| hd l
    | Branch(b, p, t1, t2) -> choose t1

(* ------------------------------------------------------------------------- *)
(* Install a trivial printer for the general polymorphic case.               *)
(* ------------------------------------------------------------------------- *)

/// Print a finite partial function.
let print_fpf(f : func<'a, 'b>) = "<func>"

#if INTERACTIVE
fsi.AddPrinter print_fpf
#endif

(* ------------------------------------------------------------------------- *)
(* Set operations parametrized by equality (from Steven Obua).               *)
(* ------------------------------------------------------------------------- *)

/// Tests if an element is equivalent to a member of a list w.r.t. some relation.
let rec mem' eq =
    let rec mem x lis = 
        match lis with
        | [] -> false
        | (h :: t) -> eq x h || mem x t
    mem

/// Insert element into list unless it contains an equivalent one already.
let insert' eq x l = 
    if mem' eq x l then l
    else x :: l

/// Union of sets modulo an equivalence.
let union' eq l1 l2 = itlist (insert' eq) l1 l2
/// Compute union of a family of sets modulo an equivalence.
let unions' eq l = itlist (union' eq) l []
/// Subtraction of sets modulo an equivalence.
let subtract' eq l1 l2 = filter (fun x -> not(mem' eq x l2)) l1

(* ------------------------------------------------------------------------- *)
(* Accepts decimal, hex or binary numeral, using C notation 0x... for hex    *)
(* and analogous 0b... for binary.                                           *)
(* ------------------------------------------------------------------------- *)

/// Converts decimal, hex or binary string representation into number.
let num_of_string = 
    let values = 
        ["0", 0;
         "1", 1;
         "2", 2;
         "3", 3;
         "4", 4;
         "5", 5;
         "6", 6;
         "7", 7;
         "8", 8;
         "9", 9;
         "a", 10;
         "A", 10;
         "b", 11;
         "B", 11;
         "c", 12;
         "C", 12;
         "d", 13;
         "D", 13;
         "e", 14;
         "E", 14;
         "f", 15;
         "F", 15]

    let rec valof b s = 
        choice {
            let! v =
                match assoc s values with
                | Some x -> Choice.result <| Int x
                | None -> Choice.failwith "find"
            if v </ b then 
                return v
            else 
                return! Choice.failwith "num_of_string: invalid digit for base"
        }

    and two = num_2
    and ten = num_10
    and sixteen = Int 16

    let rec num_of_stringlist b l = 
        choice {
            match l with
            | [] -> 
                return! Choice.failwith "num_of_string: no digits after base indicator"
            | [h] -> 
                return! valof b h
            | h :: t -> 
                let! x = valof b h 
                let! y = num_of_stringlist b t
                return x +/ b */ y
        }

    fun s ->
        choice {
            match explode(s) with
            | [] ->
                return! Choice.failwith "num_of_string: no digits"
            | "0" :: "x" :: hexdigits ->
                return! num_of_stringlist sixteen (rev hexdigits)
            | "0" :: "b" :: bindigits ->
                return! num_of_stringlist two (rev bindigits)
            | decdigits ->
                return! num_of_stringlist ten (rev decdigits)
        } : Protected<_>
            
(* ------------------------------------------------------------------------- *)
(* Convenient conversion between files and (lists of) strings.               *)
(* ------------------------------------------------------------------------- *)

/// Read file and convert content into a list of strings.
// OPTIMIZE : Use a StreamReader here instead of the OCaml-compatibility stuff.
let strings_of_file filename = 
    let fd = 
        try
            Pervasives.open_in filename
        with
        | Sys_error _ as e ->
            nestedFailwith e ("strings_of_file: can't open " + filename)
    let rec suck_lines acc = 
        try 
            let l = Pervasives.input_line fd
            suck_lines(l :: acc)
        with
        | End_of_file -> rev acc
    let data = suck_lines []
    (Pervasives.close_in fd
     data)

/// Read file and convert content into a string.
// OPTIMIZE : Use a StringBuilder and iterate over the file to avoid creating
// intermediate values all at once when we can stream them instead.
// Initialize the StringBuilder's capacity to the file size to avoid re-allocation.
let string_of_file filename = end_itlist (fun s t -> s + "\n" + t) (strings_of_file filename)

/// Write out a string to a named file.
// OPTIMIZE : Make this an alias for System.IO.File.WriteAllText().
let file_of_string filename s = 
    let fd = Pervasives.open_out filename
    output_string fd s
    close_out fd
