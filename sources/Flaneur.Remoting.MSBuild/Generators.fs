namespace Flaneur.MSBuild

open Myriad.Core

[<MyriadGenerator(nameof FlaneurRemotingGenerator)>]
type FlaneurRemotingGenerator() =

    interface IMyriadGenerator with
        member _.ValidInputExtensions: seq<string> = [ ".fs" ]
        member _.Generate(ctx : GeneratorContext) = Output.Source """namespace Cat"""