namespace Flaneur.Remoting

open Myriad.Core

[<MyriadGenerator(nameof FlaneurRemotingGenerator)>]
type FlaneurRemotingGenerator() =

    interface IMyriadGenerator with
        member _.ValidInputExtensions = Example.extensions
        member _.Generate ctx = Example.generate ctx