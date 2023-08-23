module Flaneur.Remoting.Client

open System.Diagnostics

type Encoder<'Value, 'Encoded> = 'Value -> 'Encoded

type Handler<'Parameter, 'Encoded, 'Result> =
  Encoder<'Parameter, 'Encoded>
    -> (string -> 'Result)
    -> string
    -> 'Parameter list
    -> System.IObservable<'Result>

type Encoder2<'Encoded> = System.Type -> obj -> 'Encoded
type Decoder2<'Encoded> = System.Type -> 'Encoded -> obj
type Handler2<'Encoded> =
  string
    -> 'Encoded list
    -> System.IObservable<'Encoded>

module Handler =
  open Fable.Core
  open FSharp.Control
  open Fetch

  module private Async =
    let map f x = async {
      let! y = x
      return f y
    }

    let flattenAsyncSeq (x : Async<AsyncSeq<'a>>) = asyncSeq {
      let! y = x
      yield! y
    }

  type private ReadableStreamReadResult<'T> =
    interface
      abstract value : 'T with get
      abstract ``done`` : bool with get
    end

  type private ReadableStreamDefaultReader<'T> =
    interface
      abstract read : unit -> JS.Promise<ReadableStreamReadResult<'T>>
      abstract releaseLock : unit -> unit
    end

  // TODO: Extract and improve bindings for ReadableStream API
  // https://developer.mozilla.org/en-US/docs/Web/API/ReadableStream
  [<Emit("$0.body.pipeThrough(new TextDecoderStream()).getReader()")>]
  let private getResponseBodyReader
    (response : Response)
    : ReadableStreamDefaultReader<string>
    =
    jsNative

  let create serviceOrigin : Handler<_, _, _> =
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
      |> Promise.map getResponseBodyReader
      |> Async.AwaitPromise
      |> Async.map (fun reader -> asyncSeq {
        let mutable notFinished = true

        while notFinished do
          let! result = reader.read () |> Async.AwaitPromise

          if not result.``done`` then
            yield (resultDecode result.value)
          else
            ()

          notFinished <- not result.``done``

        reader.releaseLock ()
      })
      |> Async.flattenAsyncSeq
      |> AsyncSeq.toObservable


  let create2 origin : Handler2<_> =
    fun service args ->
      let url =
        let path = $"%s{origin}/%s{service}"
        let query =
          args
          |> List.map System.Uri.EscapeDataString
          |> List.mapi (fun i v -> $"{i}={v}")
          |> String.concat "&"
        String.concat "?" [ path; if query <> "" then query ]
      
      let sequence = asyncSeq {
        let! cancel = Async.CancellationToken

        let! response = Async.AwaitPromise <| fetch url [] 
        let reader = getResponseBodyReader response
        use _ = { 
          new System.IDisposable with 
            member _.Dispose () = 
              Debug.WriteLine $"{url} : iteration disposed"
              reader.releaseLock ()
        }

        let mutable notFinished = true

        while notFinished && not cancel.IsCancellationRequested do
          Debug.WriteLine $"{url} : moving next"
          let! result = reader.read () |> Async.AwaitPromise

          if not result.``done`` then
            Debug.WriteLine $"{url} : got next"
            yield result.value
          else
            Debug.WriteLine $"{url} : no next"
            ()

          notFinished <- not result.``done``
        
        Debug.WriteLine $"{url} : iteration completed"

        reader.releaseLock ()
      }

      AsyncSeq.toObservable sequence