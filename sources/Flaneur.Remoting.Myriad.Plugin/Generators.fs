namespace Flaneur.Remoting

open Myriad.Core

[<MyriadGenerator(nameof FlaneurRemotingProxyGenerator)>]
type FlaneurRemotingProxyGenerator() =

    interface IMyriadGenerator with
        member _.ValidInputExtensions = ProxyGenerator.extensions
        member _.Generate ctx = ProxyGenerator.generate ctx

[<MyriadGenerator(nameof FlaneurRemotingHandlerGenerator)>]
type FlaneurRemotingHandlerGenerator() =

    interface IMyriadGenerator with
        member _.ValidInputExtensions = HandlerGenerator.extensions
        member _.Generate ctx = HandlerGenerator.generate ctx