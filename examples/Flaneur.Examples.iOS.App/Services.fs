module Flaneur.Examples.iOS.Services
open Flaneur.Remoting.Client
open Browser

type Animal = { Name: string; Age: int }

type FooService =
  abstract Foo: unit -> System.IObservable<{|Value:int|}>
  abstract FooWith: string * int -> System.IObservable<Animal>
    
let FooProxy serviceOrigin =
  let stringEncoder x = $"{x}"
  let inline jsonDecoder x =
    match Thoth.Json.Decode.Auto.fromString x with
    | Ok k -> k
    | Error e ->
      console.error e
      invalidOp e

  {
    new FooService with
      member _.FooWith (a0:string, a1: int) = Handler.create serviceOrigin stringEncoder jsonDecoder "fooWith" [a0; a1.ToString();]
      member _.Foo () = Handler.create serviceOrigin stringEncoder jsonDecoder "foo" []
  }

