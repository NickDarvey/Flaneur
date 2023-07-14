namespace Flaneur.MSBuild

open Myriad.Core

[<MyriadGenerator(nameof FlaneurRemotingGenerator)>]
type FlaneurRemotingGenerator() =

    interface IMyriadGenerator with
        member _.ValidInputExtensions: seq<string> = [ ".fs" ]
        member _.Generate(ctx : GeneratorContext) =
          let asdf = Myriad.Core.Generation.header
          printfn $"{asdf}"
          let ast, _ =
            Ast.fromFilename ctx.InputFilename
            |> Async.RunSynchronously
            |> Array.head
          Output.Source """namespace Dog"""