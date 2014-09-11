(*** hide ***)
#I "../../bin/v4.5/"
(**
Interactive Service: Embedding F# Interactive
=============================================

This tutorial demonstrates how to embed F# interactive in your application. F# interactive
is an interactive scripting environment that compiles F# code into highly efficient IL code
and executes it on the fly. The F# interactive service allows you to embed F# evaluation in 
your application.

> **NOTE:** There is a number of options for embedding F# Interactive. The easiest one is to use the 
`fsi.exe` process and communicate with it using standard input and standard output. In this
tutorial, we look at calling F# interactive directly through .NET API. However, if you have
no control over the input, it is a good idea to run F# interactive in a separate process.
One reason is that there is no way to handle `StackOverflowException` and so a poorly written
script can terminate the host process.

However, the F# interactive service is still useful, because you might want to wrap it in your
own executable that is then executed (and communicates with the rest of your application), or
if you only need to execute limited subset of F# code (e.g. generated by your own DSL).

Starting the F# interactive
---------------------------

First, we need to reference the libraries that contain F# interactive service:
*)

#r "FSharp.Compiler.Service.dll"
open Microsoft.FSharp.Compiler.Interactive.Shell

(**
To communicate with F# interactive, we need to create streams that represent input and
output. We will use those later to read the output printed as a result of evaluating some
F# code that prints:
*)
open System
open System.IO
open System.Text

// Intialize output and input streams
let sbOut = new StringBuilder()
let sbErr = new StringBuilder()
let inStream = new StringReader("")
let outStream = new StringWriter(sbOut)
let errStream = new StringWriter(sbErr)

// Build command line arguments & start FSI session
let argv = [| "C:\\fsi.exe" |]
let allArgs = Array.append argv [|"--noninteractive"|]

let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
let fsiSession = FsiEvaluationSession.Create(fsiConfig, allArgs, inStream, outStream, errStream)  



(**
Evaluating and executing code
-----------------------------

The F# interactive service exposes two methods that can be used for interaction. The first
is `EvalExpression` which evaluates an expression and returns its result. The result contains
the returned value (as `obj`) and the statically inferred type of the value:
*)
/// Evaluate expression & return the result
let evalExpression text =
  match fsiSession.EvalExpression(text) with
  | Some value -> printfn "%A" value.ReflectionValue
  | None -> printfn "Got no result!"
(**
The `EvalInteraction` method has no result. It can be used to evaluate side-effectful operations
such as printing, or other interactions that are not valid F# expressions, but can be entered in
the F# Interactive console. Such commands include `#time "on"` (and other directives), `open System`
and other top-level statements.
*)
/// Evaluate interaction & ignore the result
let evalInteraction text = 
  fsiSession.EvalInteraction(text)
(**
The two functions take string as an argument and evaluate (or execute) it as F# code. The code 
passed to them does not require `;;` at the end. Just enter the code that you want to execute:
*)
evalExpression "42+1"
evalInteraction "printfn \"bye\""

(**
The `EvalScript` method allows to evaluate a complete .fsx script.
*)
/// Evaluate script & ignore the result
let evalScript scriptPath = 
  fsiSession.EvalScript(scriptPath)

File.WriteAllText("sample.fsx", "let twenty = 10 + 10")
evalScript "sample.fsx"

(**
Type checking in the evaluation context
------------------

Let's assume you have a situation where you would like to typecheck code 
in the context of the F# Interactive scripting session. For example, you first
evaluation a declaration:
*)

evalInteraction "let xxx = 1 + 1"

(**

Now you want to typecheck the partially complete code `xxx + xx`
*)

let parseResults, checkResults, checkProjectResults = fsiSession.ParseAndCheckInteraction("xxx + xx")

(** 
The `parseResults` and `checkResults` have types `ParseFileResults` and `CheckFileResults`
explained in [Editor](editor.html). You can, for example, look at the type errors in the code:
*)
checkResults.Errors.Length // 1

(** 
The code is checked with respect to the logical type context available in the F# interactive session
based on the declarations executed so far.

You can also request declaration list information, tooltip text and symbol resolution:
*)
open Microsoft.FSharp.Compiler

let identToken = Parser.tagOfToken(Parser.token.IDENT("")) 
checkResults.GetToolTipTextAlternate(1, 2, "xxx + xx", ["xxx"], identToken) // a tooltip

checkResults.GetSymbolUseAtLocation(1, 2, "xxx + xx", ["xxx"]) // symbol xxx
  
(**
Exception handling
------------------

If you want to handle compiler errors in a nicer way and report a useful error message, you might
want to use something like this:
*)

try 
  evalExpression "42 + 1.0"
with e ->
  match e.InnerException with
  | null -> 
      printfn "Error evaluating expression (%s)" e.Message
  //| WrappedError(err, _) -> 
  //    printfn "Error evaluating expression (Wrapped: %s)" err.Message
  | _ -> 
      printfn "Error evaluating expression (%s)" e.Message
(**
The 'fsi' object
------------------

If you want your scripting code to be able to access the 'fsi' object, you should pass in an implementation of this object explicitly.
Normally the one fromm FSharp.Compiler.Interactive.Settings.dll is used.
*)

let fsiConfig2 = FsiEvaluationSession.GetDefaultConfiguration(fsi)

(**
Collectible code generation
------------------

Evaluating code in using FsiEvaluationSession generates a .NET dynamic assembly and uses other resources.
You can make generated code collectible by passing `collectible=true`.  However code will only
be collected if there are no outstanding object references involving types, for example
`FsiValue` objects returned by `EvalExpression`, and you must have disposed the `FsiEvaluationSession`.
See also [Restrictions on Collectible Assemblies](http://msdn.microsoft.com/en-us/library/dd554932(v=vs.110).aspx#restrictions).

The example below shows the creation of 200 evaluation sessions. Note that `collectible=true` and
`use session = ...` are both used. 

If collectible code is working correctly,
overall resource usage will not increase linearly as the evaluation progresses.
*)

let collectionTest() = 

    for i in 1 .. 200 do
        let defaultArgs = [|"fsi.exe";"--noninteractive";"--nologo";"--gui-"|]
        use inStream = new StringReader("")
        use outStream = new StringWriter()
        use errStream = new StringWriter()

        let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
        use session = FsiEvaluationSession.Create(fsiConfig, defaultArgs, inStream, outStream, errStream, collectible=true)
        
        session.EvalInteraction (sprintf "type D = { v : int }")
        let v = session.EvalExpression (sprintf "{ v = 42 * %d }" i)
        printfn "iteration %d, result = %A" i v.Value.ReflectionValue

// collectionTest()  <-- run the test like this
