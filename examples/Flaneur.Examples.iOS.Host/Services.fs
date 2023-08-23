module Flaneur.Examples.iOS.Host.Services

open FSharp.Control
open System.Diagnostics

// TODO: Share via Flaneur lib?
type Encoder2<'Encoded> = System.Type -> obj -> 'Encoded
type Decoder2<'Encoded> = System.Type -> 'Encoded -> obj

// TODO: Move to shared library
type Animal = { Name : string ; Age : int }

/// Creates a handler for the example service.
/// (This would be codegened in future.)
let createExampleServiceHandler (encodeResult : Encoder2<string>) (decodeArg : Decoder2<string>) name args =
  let teeLogResult name msg =
    Debug.WriteLine $"%s{name} result: %s{msg}"
    msg

  match name, args with
  | "/foo", [||] ->
    asyncSeq { yield {| Value = 1 |} }
    |> AsyncSeq.toObservable
    |> Observable.map (fun v -> encodeResult typeof<{| Value : int |}> (box v))
    |> Observable.map (teeLogResult "foo")
  | "/bar", [| a ; b |] ->
    let a = decodeArg typeof<string> a :?> string
    let b = decodeArg typeof<int> b :?> int
    asyncSeq {
      do! Async.Sleep 1000
      yield { Name = "Daisy" ; Age = 15 }
      do! Async.Sleep 1000
      yield { Name = "Fluffles" ; Age = 9 }
      do! Async.Sleep 1000
      yield { Name = $"Arg {a}" ; Age = b }
    }
    |> AsyncSeq.toObservable
    |> Observable.map (fun v -> encodeResult typeof<Animal> (box v))
    |> Observable.map (teeLogResult "bar")
  | "/baz", [| |] -> 
    asyncSeq {
      let! cancel = Async.CancellationToken
      do! Async.Sleep 1000
      if cancel.IsCancellationRequested then () else
      yield { Name = "Wolf" ; Age = 14 }
      do! Async.Sleep 1000
      if cancel.IsCancellationRequested then () else
      yield { Name = "Hound" ; Age = 15 }
      do! Async.Sleep 1000
      if cancel.IsCancellationRequested then () else
      yield { Name = "Dog" ; Age = 16 }
    }
    |> AsyncSeq.toObservable
    |> Observable.map (fun v -> encodeResult typeof<Animal> (box v))
    |> Observable.map (teeLogResult "baz")
  | name, args -> invalidOp $"Unknown service name '{name}' with {args.Length} arguments."