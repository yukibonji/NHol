﻿(*

Copyright 1998-2007 John Harrison
Copyright 2013 Jack Pappas, Anh-Dung Phan, Eric Taucher

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

// Note: To use this with F# Interactive within Visual Studio 2010
// In Visual Studio menu: Tool -> Options -> F# Tools -> F# Interactive
// For F# Interactive Options add --define:FSI_VER_2
#if FSI_VER_2
#r "./../packages/FSharp.Compatibility.OCaml.0.1.10/lib/net40/FSharp.Compatibility.OCaml.dll"
#r "./../packages/FSharp.Compatibility.OCaml.Format.0.1.10/lib/net40/FSharp.Compatibility.OCaml.Format.dll"
#r "./../packages/FSharp.Compatibility.OCaml.System.0.1.10/lib/net40/FSharp.Compatibility.OCaml.System.dll"
#r "./../packages/ExtCore.0.8.32/lib/net40/ExtCore.dll"
#else
#I "./../packages"

#r "FSharp.Compatibility.OCaml.0.1.10/lib/net40/FSharp.Compatibility.OCaml.dll"
#r "FSharp.Compatibility.OCaml.Format.0.1.10/lib/net40/FSharp.Compatibility.OCaml.Format.dll"
#r "FSharp.Compatibility.OCaml.System.0.1.10/lib/net40/FSharp.Compatibility.OCaml.System.dll"
#r "ExtCore.0.8.32/lib/net40/ExtCore.dll"
#endif

// Disable "Incomplete pattern matches on this expression." warnings.
// Some of these are true warnings, but should be fixed in the code.
// No sense in seeing these warnings when using F# interactive.
#nowarn "25"

// Disable "This and other recursive references to the object(s) being defined will be checked
// for initialization-soundness at runtime through the use of a delayed reference. 
// This is because you are defining one or more recursive objects, rather than recursive functions."
#nowarn "40"

// Disable "Uppercase variable identifiers should not generally be used in patterns, and may indicate a misspelt pattern name."
#nowarn "49"

// Disable ML compatibility warnings
#nowarn "62"

open FSharp.Compatibility.OCaml;;
open FSharp.Compatibility.OCaml.Num;;

(*

Gc.set { (Gc.get()) with Gc.stack_limit = 16777216 }

(* ------------------------------------------------------------------------- *)
(* Make sure user interrupts generate an exception, not kill the process.    *)
(* ------------------------------------------------------------------------- *)

Sys.catch_break true

(* ------------------------------------------------------------------------- *)
(* Set up a quotation expander for the `...` quotes.                         *)
(* This includes the case `;...` to support miz3, even if that isn't loaded. *)
(* ------------------------------------------------------------------------- *)

let quotexpander s = 
    if s = "" then failwith "Empty quotation"
    else 
        let c = String.sub s 0 1
        if c = ":" then "parse_type \"" + (String.escaped(String.sub s 1 (String.length s - 1))) + "\""
        elif c = ";" then "parse_qproof \"" + (String.escaped s) + "\""
        else "parse_term \"" + (String.escaped s) + "\""

Quotation.add "tot" (Quotation.ExStr(fun x -> quotexpander))

(* ------------------------------------------------------------------------- *)
(* Modify the lexical analysis of uppercase identifiers.                     *)
(* ------------------------------------------------------------------------- *)

set_jrh_lexer

(* ------------------------------------------------------------------------- *)
(* Load in the bignum library and set up printing in the toplevel.           *)
(* ------------------------------------------------------------------------- *)

#load "nums.cma"

include Num

let print_num n = 
    Format.open_hbox()
    Format.print_string(string_of_num n)
    Format.close_box()

fsi.AddPrinter print_num

*)

// Start NLog setup section

#if FSI_VER_2
#r @"./../packages/NLog.2.0.1.2/lib/net40/NLog.dll"
#else
// Change from package to NHol solution. Not NHol project.
#r @"NLog.2.0.1.2/lib/net40/NLog.dll"
#endif

// Configure and intialize NLog for logging

let configureNLog () =
  // If you copy or move this be sure to verify directory path is correct
  // because __SOURCE_DIRECTORY__ may change.
  let projectPath = __SOURCE_DIRECTORY__ + @".\..\NHol"
  let configPath = projectPath + @"\NLog.config"
  printfn "NLog config: %s" (System.IO.Path.GetFullPath configPath)
  let xmlConfig = new NLog.Config.XmlLoggingConfiguration(configPath)
  NLog.LogManager.Configuration <- xmlConfig

// Note: How to set Layout property in F#
// If you look at C# examples of setting the layout property it will look like
// fileTarget.Layout = "${time} ${message}"
// which in F# would be
// fileTarget.Layout <- "${time} ${message}"
// but this does not work with F# as you will get
// The type 'string' is not compatible with the type 'NLog.Layouts.Layout'
// To do this property you have to construct a SimpleLayout with the text as a parameter
// then convert the SimpleLayout to a Layout before assiging.

// Note: __SOURCE_DIRECTORY__ can cause problems if you don't understand
// how it works.
//
// If used from F# Interactive window in VS you will get something like
// C:\Users\Eric.Windows7Ult\AppData\Local\Temp
//
// If sent to F# Interactive window in VS from fsx script you will get the
// directory the where the fsx script is located.
//
// If used in fsi.exe, running fsi.exe from a command prompt, you will get
// the directory where fsi.exe is located.
//
// If fsi.exe is started from a Windows Explorer search, you will get
// C:\Windows\system32
//
// Since we are using __SOURCE_DIRECTORY__ to locate a config file
// then depending on where __SOURCE_DIRECTORY__ is located, or
// how fsi.exe is started, the value returned can change.
//
// So if you move a file or reaname a file or directory in the path
// or copy a function with __SOURCE_DIRECTORY__ in it,
// don't be surpised if the function fails related to the
// __SOURCE_DIRECTORY__ value changing.

let configureNLogPrgramatically () =
  let config = new NLog.Config.LoggingConfiguration()
  // Setup targets
  // Log to a file
  let fileTarget = new NLog.Targets.FileTarget()
  let solutionPath = __SOURCE_DIRECTORY__  + @".\..\"
  let filePath = solutionPath + @"NHol.run.Nlog.txt"
  let layout = new NLog.Layouts.SimpleLayout(filePath)
  fileTarget.FileName <- layout
  fileTarget.Name <- "file"
  fileTarget.AutoFlush <- true
  let simpleLayout = new NLog.Layouts.SimpleLayout("${time} ${callsite} ${level} ${message}")
  let (layout : NLog.Layouts.Layout) = simpleLayout :> NLog.Layouts.Layout
  fileTarget.Layout <- layout
  config.AddTarget("file", fileTarget)
  // Log to the console
  let consoleTarget = new NLog.Targets.ConsoleTarget()
  consoleTarget.Name <- "console"
  let simpleLayout = new NLog.Layouts.SimpleLayout("${time} ${callsite} ${level} ${message}")
  let (layout : NLog.Layouts.Layout) = simpleLayout :> NLog.Layouts.Layout
  consoleTarget.Layout <- layout
  config.AddTarget("console", consoleTarget)
  // Setup Rules
  let rule1 = new NLog.Config.LoggingRule("*",NLog.LogLevel.Trace,fileTarget)
  config.LoggingRules.Add(rule1)
  let rule2 = new NLog.Config.LoggingRule("*",NLog.LogLevel.Trace,consoleTarget)
  config.LoggingRules.Add(rule2)
  NLog.LogManager.Configuration <- config

let printNLogConfig () =
  let targets = NLog.LogManager.Configuration.AllTargets
  let rec processTargets targets acc =
    match targets with
    | target :: tl ->
      match (target : NLog.Targets.Target) with
      | :? NLog.Targets.FileTarget as fileTarget ->
          let autoFlush = "autoFlush: " + fileTarget.AutoFlush.ToString()
          let createDirs = "createDirs: " + fileTarget.CreateDirs.ToString()
          let encoding = "encoding: " + fileTarget.Encoding.ToString()
          let filename = "filename: " + fileTarget.FileName.ToString()
          let keepFileOpen = "keepFileOpen: " + fileTarget.KeepFileOpen.ToString()
          let lineEnding = "lineEnding: " + fileTarget.LineEnding.ToString()
          let footer = "footer: " + if (fileTarget.Footer<> null) then fileTarget.Footer.ToString() else "None"
          let header = "header: " + if (fileTarget.Header<> null) then fileTarget.Header.ToString() else "None"
          let layout = "layout: " + if (fileTarget.Layout<> null) then fileTarget.Layout.ToString() else "None"
          let name = "name: " + target.Name
          let acc = ("[" + name + ", " + layout + ", " + header + ", " + footer + ", " + autoFlush + ", " + createDirs+ ", " + encoding+ ", " + filename+ ", " + keepFileOpen + ", " + lineEnding + "]") :: acc
          processTargets tl acc
      | :? NLog.Targets.ConsoleTarget as consoleTarget ->
          let error = consoleTarget.Error.ToString()
          let footer = "footer: " + if (consoleTarget.Footer<> null) then consoleTarget.Footer.ToString() else "None"
          let header = "header: " + if (consoleTarget.Header<> null) then consoleTarget.Header.ToString() else "None"
          let layout = "layout: " + if (consoleTarget.Layout<> null) then consoleTarget.Layout.ToString() else "None"
          let name = "name: " + target.Name
          let acc = ("[" + name + ", " + layout + ", " + header + ", " + footer + ", " + error + "]") :: acc
          processTargets tl acc
      | :? NLog.Targets.TargetWithLayoutHeaderAndFooter as targetWithHeaders ->
          let footer = "footer: " + if (targetWithHeaders.Footer<> null) then targetWithHeaders.Footer.ToString() else "None"
          let header = "header: " + if (targetWithHeaders.Header<> null) then targetWithHeaders.Header.ToString() else "None"
          let layout = "layout: " + if (targetWithHeaders.Layout<> null) then targetWithHeaders.Layout.ToString() else "None"
          let name = "name: " + target.Name
          let acc = ("[" + name + ", " + layout + ", " + header + ", " + footer + "]") :: acc
          processTargets tl acc
      | :? NLog.Targets.TargetWithLayout as targetWithLayout ->
          let layout = "layout: " + if (targetWithLayout.Layout<> null) then targetWithLayout.Layout.ToString() else "None"
          let name = "name: " + target.Name
          let acc = ("[" + name + ", " + layout + "]") :: acc
          processTargets tl acc
      | _ ->
          let name = target.Name
          let acc = ("[" + name + "]") :: acc
          processTargets tl acc
    | [] -> List.rev acc
  let rules = NLog.LogManager.Configuration.LoggingRules
  let listToString (list : string list) =
      let rec processList list acc =
        match list with
        | (item : string) :: tl ->
            match acc with
            | "" ->
                let acc = item
                processList tl acc
            | _ ->
                let acc = acc + ", " + item
                processList tl acc
        | [] -> "[" + acc + "]"
      processList list ""
  let rec processRules rules acc =
    match rules with
    | (rule : NLog.Config.LoggingRule) :: tl ->
        let name = "name: " + rule.LoggerNamePattern
        let levels = "levels: " + (rule.Levels
            |> Seq.map (fun (level : NLog.LogLevel) -> level.Name)
            |> Seq.toList
            |> listToString)
        let targets = "targets: " + (rule.Targets
            |> Seq.map (fun (target : NLog.Targets.Target) -> target.Name)
            |> Seq.toList
            |> listToString)
        let filters = "filters: " + (rule.Filters
            |> Seq.map (fun (fliter : NLog.Filters.Filter) -> fliter.Action.ToString())
            |> Seq.toList
            |> listToString)
        let final = "final: " + rule.Final.ToString()
        let acc = ("[" + name + ", " + levels + ", " + targets + ", " + filters + ", " + final + "]") :: acc
        processRules tl acc
    | [] -> List.rev acc
  let targetList =  targets |> Seq.cast |> Seq.toList
  let targetTextList = (processTargets targetList []) |> listToString
  let ruleList =  rules |> Seq.cast |> Seq.toList
  let ruleTextList = (processRules ruleList []) |> listToString
  printfn "targets: %A" targetTextList
  printfn "rules: %A" ruleTextList

#if INTERACTIVE
// Note: This is what is used with HOL Light command prompt.
// Since we can't rely on __SOURCE_DIRECTORY__ to get to NLog.config,
// we create the configuration in the code.
configureNLogPrgramatically ()
#else
configureNLog ()
#endif

// Setup logger
let logger = NLog.LogManager.GetLogger("file")
logger.Trace("NLog message from system.fsx")

// End NLog setup section
