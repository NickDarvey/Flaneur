module Lit.TodoMVC.Services

module Proxy = 
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

    let flattenAsyncSeq (x: Async<AsyncSeq<'a>>) = asyncSeq {
      let! y = x
      yield! y
    }

  [<Emit("$0.body.pipeThrough(new TextDecoderStream()).getReader()")>]
  let private getResponseBodyReader (response: Response) : JS.Promise<ReadableStreamDefaultReader<string>> = jsNative

  let queryEncode serviceName args =
    let query =
      if List.length args = 0 then "" else 
      let argsString =
        args
        |> List.mapi (fun index value -> $"a{index}={value}")
        |> String.concat "&"
      $"?{argsString}"
  
    $"{serviceName}{query}"

  // TODO: implement decode properly
  let decode = id

  let invoke serviceOrigin serviceName args= 
    let endpoint = queryEncode serviceName args
    fetch $"{serviceOrigin}/{endpoint}" List.empty
    |> Promise.bind getResponseBodyReader
    |> Async.AwaitPromise
    |> Async.map (fun reader -> asyncSeq {
      let mutable notFinished = true
      while notFinished do 
        let! result = reader.read () |> Async.AwaitPromise    
        if not result.``done`` then yield result.value else ()
        notFinished <- not result.``done``
      reader.releaseLock ()
    })
    |> Async.flattenAsyncSeq
    |> AsyncSeq.toObservable
    |> Observable.map decode


type FooService =
  abstract Foo: unit -> System.IObservable<string>
  abstract FooWith: string * int -> System.IObservable<string>

let FooProxy serviceOrigin = {
  new FooService with  
  member _.Foo () = Proxy.invoke serviceOrigin "foo" []
  member _.FooWith (a0:string, a1: int) = Proxy.invoke serviceOrigin "fooWith" [a0; a1.ToString();] 
}

