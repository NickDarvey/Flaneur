module Flaneur.Remoting.Proxies

open System.Diagnostics

open Fable.Streams
open Fable.Encoding

type Encoder<'Encoded> = System.Type -> obj -> 'Encoded
type Decoder<'Encoded> = System.Type -> 'Encoded -> obj

module HTTP =
  open Fable.Core
  open FSharp.Control
  open Fetch

  /// Creates an HTTP method invocation proxy.
  let create () =
    // Hosts like iOS need some help distinguishing unique requests.
    let mutable invocation = 0UL
    fun service args ->
      invocation <- invocation + 1UL

      let url =
        let path = $"remoting://main/%s{service}"
        let query =
          args
          |> List.map System.Uri.EscapeDataString
          |> List.mapi (fun i v -> $"{i}={v}")
          |> List.append [ $"$invocation=%i{invocation}"]
          |> String.concat "&"
        String.concat "?" [ path; if query <> "" then query ]
      
      let sequence = asyncSeq {
        Debug.WriteLine $"{url} : iteration started"

        let! response = Async.AwaitPromise <| fetch url [] 
        let reader = response.body.pipeThrough(TextDecoderStream.Create()).getReader()

        use _ = { 
          new System.IDisposable with 
            member _.Dispose () = 
              Debug.WriteLine $"{url} : iteration disposed"
              reader.cancel () |> ignore
        }

        let mutable notFinished = true

        while notFinished  do
          let! result = reader.read () |> Async.AwaitPromise

          if not result.``done`` then
            yield result.value
          else
            ()

          notFinished <- not result.``done``
        
        Debug.WriteLine $"{url} : iteration completed"

      }

      AsyncSeq.toObservable sequence

// TODO select correct proxy implementation based on platform.
// (A Flaneur.Wapp lib might need to help us by making env variables like this available.)