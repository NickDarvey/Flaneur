module Flaneur.Remoting.ProxyGenerator

open Myriad.Core

let extensions = [ ".fs" ]

let generate (ctx : GeneratorContext) =
  Output.Source """namespace ProxyTestNamespace"""



    