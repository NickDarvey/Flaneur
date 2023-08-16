module Flaneur.Remoting.Client

open Browser

type ProxyInvoke<'Result> = string * string list -> System.IObservable<'Result>

type Encoder<'Encoded> =
  abstract Encode : 'Value -> 'Encoded

type Decoder<'Encoded,'Value> =
  abstract Decode : 'Encoded -> 'Value

type Handler<'Parameter,'Result,'Value> = Encoder<'Parameter> -> Decoder<'Parameter,'Value> -> string (*action name*) -> 'Parameter list -> System.IObservable<'Result>

module Handler = 
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
      
  let create serviceOrigin =
    fun argEncode resultDecode serviceName args ->
      let endPoint =
        if List.length args = 0 then
          $"{serviceOrigin}/{serviceName}"
        else
          let queryParam = 
            args
            |> List.mapi (fun index value -> $"a{index}={argEncode value}")
            |> String.concat "&"
          $"{serviceOrigin}/{serviceName}?{queryParam}"

      fetch endPoint List.empty
      |> Promise.bind getResponseBodyReader
      |> Async.AwaitPromise
      |> Async.map (fun reader -> asyncSeq {
        let mutable notFinished = true
        while notFinished do 
          let! result = reader.read () |> Async.AwaitPromise    
          if not result.``done`` then
            yield (resultDecode result.value)
          else ()
          notFinished <- not result.``done``
        reader.releaseLock ()
      })
      |> Async.flattenAsyncSeq
      |> AsyncSeq.toObservable

