module Flaneur.Remoting.Client

type Encoder<'Value, 'Encoded> = 'Value -> 'Encoded
type Decoder<'Encoded> = System.Type -> 'Encoded -> Result<obj, string>

type Handler<'Parameter,'Encoded,'Result> =
  Encoder<'Parameter,'Encoded> -> (string -> 'Result) -> string -> 'Parameter list -> System.IObservable<'Result>

module Handler = 
  open Fable.Core
  open FSharp.Control
  open Fetch

  module private Async =
    let map f x = async {
      let! y = x
      return f y
    }
  
    let flattenAsyncSeq (x: Async<AsyncSeq<'a>>) = asyncSeq {
      let! y = x
      yield! y
    }

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

  // TODO: Extract and improve bindings for ReadableStream API
  // https://developer.mozilla.org/en-US/docs/Web/API/ReadableStream
  [<Emit("$0.body.pipeThrough(new TextDecoderStream()).getReader()")>]
  let private getResponseBodyReader (response: Response) : JS.Promise<ReadableStreamDefaultReader<string>> = jsNative
      
  let create serviceOrigin : Handler<_,_,_>=
    fun argEncode resultDecode serviceName args ->
      let endPoint =
        if List.length args = 0 then
          $"%s{serviceOrigin}/%s{serviceName}"
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
