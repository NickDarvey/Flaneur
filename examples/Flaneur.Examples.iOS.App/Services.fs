module Flaneur.Examples.iOS.App.Services

open Flaneur.Remoting.Client

// TODO: Move to shared library
type Animal = { Name : string ; Age : int }

// TODO: Move to shared library
type ExampleService =
  abstract foo : unit -> System.IObservable<{| Value : int |}>
  abstract bar : string * int -> System.IObservable<Animal>
  abstract baz : unit -> System.IObservable<Animal>

/// Creates a handler for the example service.
/// (This would be codegened in future.)
let createExampleServiceProxy (encodeArg : Encoder2<string>) (decodeResult : Decoder2<string>) origin =
  { new ExampleService with
      member _.foo () =
        Handler.create2
          origin
          "foo"
          [ ]
        |> Observable.map (unbox <| decodeResult typeof<{| Value : int |}>)

      member _.bar (a0 : string, a1 : int) =
        Handler.create2
          origin
          "bar"
          [ encodeArg typeof<string> a0 ; encodeArg typeof<int> a1 ]
        |> Observable.map (unbox <| decodeResult typeof<Animal>)

      member _.baz () =
        Handler.create2
          origin
          "baz"
          [ ]
        |> Observable.map (unbox <| decodeResult typeof<Animal>)
        
  }
  