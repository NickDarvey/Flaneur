module Flaneur.Examples.iOS.App.Services

open Flaneur.Remoting.Proxies

// TODO: Move to shared library
type Animal = { Name : string ; Age : int }

// TODO: Move to shared library
type ExampleService =
  abstract foo : unit -> System.IObservable<{| Value : int |}>
  abstract bar : string * int -> System.IObservable<Animal>
  abstract baz : unit -> System.IObservable<Animal>

/// Creates a handler for the example service.
/// (This would be codegened in future.)
let createExampleServiceProxy (encodeArg : Encoder<string>) (decodeResult : Decoder<string>) origin =
  let invoke = HTTP.invokeWith origin
  { new ExampleService with
      member _.foo () =
        invoke "foo" [ ]
        |> Observable.map (unbox <| decodeResult typeof<{| Value : int |}>)

      member _.bar (a0 : string, a1 : int) =
        let args = [ encodeArg typeof<string> a0 ; encodeArg typeof<int> a1 ]
        invoke "bar" args
        |> Observable.map (unbox <| decodeResult typeof<Animal>)

      member _.baz () =
        invoke "baz" [ ]
        |> Observable.map (unbox <| decodeResult typeof<Animal>)
        
  }
  