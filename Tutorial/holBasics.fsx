#load "hol.fsx"

open NHol.lib
open NHol.fusion
open NHol.basics
open NHol.nets
open NHol.printer
open NHol.preterm
open NHol.parser
open NHol.equal
open NHol.bool
open NHol.drule
open NHol.tactics
open NHol.itab
open NHol.simp
open NHol.theorems
open NHol.ind_defs
open NHol.``class``
open NHol.trivia
open NHol.canon
open NHol.meson
open NHol.quot
open NHol.pair
open NHol.nums
open NHol.recursion
open NHol.arith   
open NHol.wf
open NHol.calc_num

//* Initial databases status *//

types();;                                                                           // internal database the_type_constants
//val it : (string * int) list = [("bool", 0); ("fun", 2)]

constants();;                                                                       // internal database the_term_constants
//val it : (string * hol_type) list =
//  [("=",
//    Tyapp ("fun",[Tyvar "A"; Tyapp ("fun",[Tyvar "A"; Tyapp ("bool",[])])]))]

axioms();;                                                                          // internal database the_axioms
//val it : thm list = []

definitions();;                                                                     // internal database the_definitions
//val it : thm list = []


//* Bool Module *//

mk_iff;; // forces bool module evaluation

types();;                                                                           // the_type_constants database doesn't change
constants();;                                                                       // new boolean constants
//  [("?!", (A->bool)->bool); ("~", bool->bool); ("F", bool);
//   ("\/", bool->bool->bool); ("?", (A->bool)->bool); ("!", (A->bool)->bool);
//   ("==>", bool->bool->bool); ("/\", bool->bool->bool); ("T", bool);
//   ("=", A->A->bool)]
axioms();;                                                                          // the_axioms database doesn't change
definitions();;                                                                     // the_definitions doesn't change

// after population of constants this parses
parse_term @"~(p /\ q) <=> ~p \/ ~q";;


//* Drule Module *//

MK_CONJ;; // forces drule module evaluation

types();;                                                                           // the_type_constants database doesn't change
constants();;                                                                       // the_term_constants database doesn't change
axioms();;                                                                          // the_axioms database doesn't change
definitions();;                                                                     // new boolean definitions
//  [|- (?!) = (\P. (?) P /\ (!x y. P x /\ P y ==> x = y));
//   |- (~) = (\p. p ==> F); |- F <=> (!p. p);
//   |- (\/) = (\p q. !r. (p ==> r) ==> (q ==> r) ==> r);
//   |- (?) = (\P. !q. (!x. P x ==> q) ==> q); |- (!) = (\P. P = (\x. T));
//   |- (==>) = (\p q. p /\ q <=> p);
//   |- (/\) = (\p q. (\f. f p q) = (\f. f T T)); |- T <=> (\p. p) = (\p. p)]


//* Tactitcs Module *//

_FALSITY_;; // forces tactics module evaluation

types();;                                                                           // the_type_constants database doesn't change
constants();;                                                                       // new constant _FALSITY_
//  [("_FALSITY_", bool); ("?!", (A->bool)->bool); ("~", bool->bool);
//   ("F", bool); ("\/", bool->bool->bool); ("?", (A->bool)->bool);
//   ("!", (A->bool)->bool); ("==>", bool->bool->bool); ("/\", bool->bool->bool);
//   ("T", bool); ("=", A->A->bool)]
axioms();;                                                                          // the_axioms database doesn't change
definitions();;                                                                     // new _FALSITY_ definition
//  [|- _FALSITY_ <=> F; |- (?!) = (\P. (?) P /\ (!x y. P x /\ P y ==> x = y));
//   |- (~) = (\p. p ==> F); |- F <=> (!p. p);
//   |- (\/) = (\p q. !r. (p ==> r) ==> (q ==> r) ==> r);
//   |- (?) = (\P. !q. (!x. P x ==> q) ==> q); |- (!) = (\P. P = (\x. T));
//   |- (==>) = (\p q. p /\ q <=> p);
//   |- (/\) = (\p q. (\f. f p q) = (\f. f T T)); |- T <=> (\p. p) = (\p. p)]

//* Itab Module *//
ITAUT_TAC;; // forces itab module evaluation
// No Changes in internal databases status

//* Simp Module *//
mk_rewrites;; // forces simp module evaluation
// No Changes in internal databases status: to be checked better

//* Theorems Module *//
EQ_REFL;; // forces theorems module evaluation
// No Changes in internal databases status: to be checked better

//* ind_defs Module *//
EXISTS_EQUATION;; // forces ind_defs module evaluation
// No Changes in internal databases status: to be checked better

// Class Module *//

ETA_AX;;    // forces class module evaluation

types();;                                                                           // the_type_constants database doesn't change
constants();;                                                                       // new constants COND, @
//  [("COND", bool->A->A->A); ("@", (A->bool)->A);
//   ("_FALSITY_", bool); ("?!", (A->bool)->bool); ("~", bool->bool);
//   ("F", bool); ("\/", bool->bool->bool); ("?", (A->bool)->bool);
//   ("!", (A->bool)->bool); ("==>", bool->bool->bool); ("/\", bool->bool->bool);
//   ("T", bool); ("=", A->A->bool)]
axioms();;                                                                          // new axioms: ETA, SELECT
//  [|- !P x. P x ==> P ((@) P); |- !t. (\x. t x) = t]
definitions();;                                                                     // new COND definition
//  [|- COND = (\t t1 t2. @x. ((t <=> T) ==> x = t1) /\ ((t <=> F) ==> x = t2));
//   |- _FALSITY_ <=> F; |- (?!) = (\P. (?) P /\ (!x y. P x /\ P y ==> x = y));
//   |- (~) = (\p. p ==> F); |- F <=> (!p. p);
//   |- (\/) = (\p q. !r. (p ==> r) ==> (q ==> r) ==> r);
//   |- (?) = (\P. !q. (!x. P x ==> q) ==> q); |- (!) = (\P. P = (\x. T));
//   |- (==>) = (\p q. p /\ q <=> p);
//   |- (/\) = (\p q. (\f. f p q) = (\f. f T T)); |- T <=> (\p. p) = (\p. p)]

// Trivia Module *//

o_DEF;; // forces trivia module evaluation

types();;                                                                           // new type 1
//  [("1", 0); ("bool", 0); ("fun", 2)]
constants();;                                                                       // new constants one, one_REP, one_ABS, I, o
//  [("one", 1); ("one_REP", 1->bool); ("one_ABS", bool->1); ("I", A->A);
//   ("o", (B->C)->(A->B)->A->C); ("COND", bool->A->A->A); ("@", (A->bool)->A);
//   ("_FALSITY_", bool); ("?!", (A->bool)->bool); ("~", bool->bool);
//   ("F", bool); ("\/", bool->bool->bool); ("?", (A->bool)->bool);
//   ("!", (A->bool)->bool); ("==>", bool->bool->bool); ("/\", bool->bool->bool);
//   ("T", bool); ("=", A->A->bool)]
axioms();;                                                                          // the_axioms database doesn't change
definitions();;                                                                     // new definitions one, I, (o)
//  [|- one = (@x. T); |- I = (\x. x); |- (o) = (\f g x. f (g x));
//   |- COND = (\t t1 t2. @x. ((t <=> T) ==> x = t1) /\ ((t <=> F) ==> x = t2));
//   |- _FALSITY_ <=> F; |- (?!) = (\P. (?) P /\ (!x y. P x /\ P y ==> x = y));
//   |- (~) = (\p. p ==> F); |- F <=> (!p. p);
//   |- (\/) = (\p q. !r. (p ==> r) ==> (q ==> r) ==> r);
//   |- (?) = (\P. !q. (!x. P x ==> q) ==> q); |- (!) = (\P. P = (\x. T));
//   |- (==>) = (\p q. p /\ q <=> p);
//   |- (/\) = (\p q. (\f. f p q) = (\f. f T T)); |- T <=> (\p. p) = (\p. p)]


//* Canon Module *//
CONJ_ACI_RULE;; // forces canon module evaluation
// No Changes in internal databases status: but maybe other changes

//* Meson Module *//
ASM_MESON_TAC;; // forces meson module evaluation
// No Changes in internal databases status: but maybe other changes

//* Quot Module *//
lift_function;; // forces quot module evaluation
// No Changes in internal databases status: but maybe other changes

//* Pair Module *//

LET_DEF;; // forces pair module evaluation: TO BE CHECKED because there is an unsolved goal TAC_PROOF

types();;                                                                           // new type prod
//  [("prod", 2); ("1", 0); ("bool", 0); ("fun", 2)]
constants();;                                                                       // lots of new constants
//  [("PASSOC", ((A#B)#C->D)->A#B#C->D); ("UNCURRY", (A->B->C)->A#B->C);
//   ("CURRY", (A#B->C)->A->B->C); ("SND", A#B->B); ("FST", A#B->A);
//   (",", A->B->A#B); ("REP_prod", A#B->A->B->bool);
//   ("ABS_prod", (A->B->bool)->A#B); ("mk_pair", A->B->A->B->bool);
//   ("_FUNCTION", (?28823->?28826->bool)->?28823->?28826);
//   ("_MATCH", ?28801->(?28801->?28804->bool)->?28804);
//   ("_GUARDED_PATTERN", bool->bool->bool->bool);
//   ("_UNGUARDED_PATTERN", bool->bool->bool);
//   ("_SEQPATTERN",
//    (?28759->?28756->bool)->(?28759->?28756->bool)->?28759->?28756->bool);
//   ("GEQ", A->A->bool); ("GABS", (A->bool)->A); ("LET_END", A->A);
//   ("LET", (A->B)->A->B); ("one", 1); ("one_REP", 1->bool);
//   ("one_ABS", bool->1); ("I", A->A); ("o", (B->C)->(A->B)->A->C);
//   ("COND", bool->A->A->A); ("@", (A->bool)->A); ("_FALSITY_", bool);
//   ("?!", (A->bool)->bool); ("~", bool->bool); ("F", bool);
//   ("\/", bool->bool->bool); ("?", (A->bool)->bool); ("!", (A->bool)->bool);
//   ("==>", bool->bool->bool); ("/\", bool->bool->bool); ("T", bool);
//   ("=", A->A->bool)]
axioms();;                                                                          // no new axioms
//  [|- !P x. P x ==> P ((@) P); |- !t. (\x. t x) = t]
definitions();;                                                                     // lots of new definitions
//  [|- PASSOC =
//   (\_1099 _1100. _1099 ((FST _1100,FST (SND _1100)),SND (SND _1100)));
//   |- UNCURRY = (\_1082 _1083. _1082 (FST _1083) (SND _1083));
//   |- CURRY = (\_1061 _1062 _1063. _1061 (_1062,_1063));
//   |- SND = (\p. @y. ?x. p = x,y); |- FST = (\p. @x. ?y. p = x,y);
//   |- (,) = (\x y. ABS_prod (mk_pair x y));
//   |- mk_pair = (\x y a b. a = x /\ b = y);
//   |- _FUNCTION = (\r x. if (?!) (r x) then (@) (r x) else @z. F);
//   |- _MATCH = (\e r. if (?!) (r e) then (@) (r e) else @z. F);
//   |- _GUARDED_PATTERN = (\p g r. p /\ g /\ r);
//   |- _UNGUARDED_PATTERN = (\p r. p /\ r);
//   |- _SEQPATTERN = (\r s x. if ?y. r x y then r x else s x);
//   |- GEQ = (\a b. a = b); |- GABS = (\P. (@) P); |- LET_END = (\t. t);
//   |- LET = (\f x. f x); |- one = (@x. T); |- I = (\x. x);
//   |- (o) = (\f g x. f (g x));
//   |- COND = (\t t1 t2. @x. ((t <=> T) ==> x = t1) /\ ((t <=> F) ==> x = t2));
//   |- _FALSITY_ <=> F; |- (?!) = (\P. (?) P /\ (!x y. P x /\ P y ==> x = y));
//   |- (~) = (\p. p ==> F); |- F <=> (!p. p);
//   |- (\/) = (\p q. !r. (p ==> r) ==> (q ==> r) ==> r);
//   |- (?) = (\P. !q. (!x. P x ==> q) ==> q); |- (!) = (\P. P = (\x. T));
//   |- (==>) = (\p q. p /\ q <=> p);
//   |- (/\) = (\p q. (\f. f p q) = (\f. f T T)); |- T <=> (\p. p) = (\p. p)]


//* Num Module *//

ONE_ONE;; // forces num module evaluation