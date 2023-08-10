module Lit.TodoMVC.Services

open Fetch
open Fable.Core
open FSharp.Control

type private ReadableStreamReadResult<'T> =
  interface
    abstract value: 'T with get
    abstract ``done``: bool with get
  end 

type private ReadableStreamDefaultReader<'T> =
  interface
    abstract read: unit -> JS.Promise<ReadableStreamReadResult<'T>>
    abstract releaseLock: unit -> unit
  end

module private Async =
  let map f x = async {
    let! y = x
    return f y
  }

  let flattenAsyncSeq (x: Async<AsyncSeq<'T>>) = asyncSeq {
    let! y = x
    yield! y
  }

[<Emit("$0.body.pipeThrough(new TextDecoderStream()).getReader()")>]
let private getResponseBodyReader (response: Response) : JS.Promise<ReadableStreamDefaultReader<string>> = jsNative

let private invoke (encode: string -> 'Args -> string) (decode: string -> 'Result) serviceName args = 
  let endpoint = encode serviceName args
  fetch endpoint List.empty
  |> Promise.bind getResponseBodyReader
  |> Async.AwaitPromise
  |> Async.map (fun reader -> asyncSeq {
    let mutable notFinished = true 
    while notFinished do 
      let! result = reader.read () |> Async.AwaitPromise
      yield result.value
      notFinished <- result.``done``
    
    reader.releaseLock ()
  })
  |> Async.flattenAsyncSeq
  |> AsyncSeq.toObservable
  |> Observable.map decode
  
let queryEncode serviceName args =
  let query =
    if List.length args = 0 then "" else 
    let argsString =
      args
      |> List.mapi (fun index value -> $"a{index}={value}")
      |> String.concat "&"
    $"?{argsString}"

  $"{serviceName}{query}"

type FooService =
  abstract Foo: unit -> System.IObservable<string>
  abstract FooWith: string * int -> System.IObservable<string>

let FooProxy = {
  new FooService with  
  member _.Foo () = invoke queryEncode id "foo" []
  member _.FooWith (a0:string, a1: int) = invoke queryEncode id "fooWith" [a0, a1.ToString()] 
}

