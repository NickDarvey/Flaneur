module Flaneur.Examples.iOS.Services

open Flaneur.Remoting.Client

type Animal = { Name: string; Age: int }

type FooService =
  abstract Foo: unit -> System.IObservable<{|Value:int|}>
  abstract FooWith: string * int -> System.IObservable<Animal>

let FooProxy serviceOrigin =
  let decoder t str : Result<obj, string> =
    let decoder = Thoth.Json.Decode.Auto.generateBoxedDecoder t |> Thoth.Json.Decode.unboxDecoder
    Thoth.Json.Decode.fromString decoder str

  let stringEncoder x = $"{x}"

  let inline decode typ str =
    match decoder typ str with
    | Ok result -> unbox result
    | Error e -> failwith $"Oops"

  {
    new FooService with
      member _.FooWith (a0:string, a1: int) = Handler.create serviceOrigin stringEncoder (decode typeof<Animal>) "fooWith" [a0; a1.ToString();]
      member _.Foo () = Handler.create serviceOrigin stringEncoder (decode typeof<{|Value:int|}>)  "foo" []
  }
