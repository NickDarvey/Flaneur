namespace Flaneur.Remoting.MSBuild

open Myriad.Core
open Flaneur.Remoting

[<MyriadGenerator(nameof FlaneurRemotingGenerator)>]
type FlaneurRemotingGenerator() =

    interface IMyriadGenerator with
        member _.ValidInputExtensions = Example.extensions
        member _.Generate ctx = Example.generate ctx