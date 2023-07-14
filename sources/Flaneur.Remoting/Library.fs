namespace Flaneur.Remoting

open Myriad.Core

module Example =
  let extensions = [ ".fs" ]
  let generate (ctx : GeneratorContext) =
    let asdf = Myriad.Core.Generation.header
    printfn $"{asdf}"
    let ast, _ =
      Ast.fromFilename ctx.InputFilename
      |> Async.RunSynchronously
      |> Array.head
    Output.Source """namespace Dog"""