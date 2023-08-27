module Flaneur.Examples.iOS.App.Main

open Lit
open Fable.Core.JS

let private decodeResult t str =
  let decoder = Thoth.Json.Decode.Auto.generateBoxedDecoderCached t

  match Thoth.Json.Decode.fromString decoder str with
  | Ok result -> result
  | Error e ->
    raise <| exn $"Failed to decode response. {e}"

let private encodeArg _ arg =
  string arg

let proxy = Services.createExampleServiceProxy encodeArg decodeResult "delegate://main"

[<LitElement("my-app")>]
let MyApp () =
  let _ = LitElement.init (fun cfg -> cfg.useShadowDom <- false)

  let subscription, setSubscription = Hook.useState Hook.emptyDisposable
  let logs, setLogs = Hook.useState []

  Hook.useEffectOnce (fun () -> subscription)

  
  let inline run (f : 'a -> System.IObservable<'b>) arg () =
    let mutable logs = []
    setLogs logs
    subscription.Dispose ()
    let subscription' = f(arg).Subscribe({
      new System.IObserver<_> with
        member _.OnNext v =
          logs <- JSON.stringify v :: logs
          setLogs logs
        member _.OnError e =
          logs <- $"%A{e}" :: logs
          setLogs logs
        member _.OnCompleted () =
          logs <- "Completed" :: logs
          setLogs logs
    })
    setSubscription subscription'

  let cancel () =
    subscription.Dispose ()
    setLogs ("Cancelled" :: logs)

  let logs = logs |> List.map (fun log -> html $"""<li>{log}</li>""")

  html
    $"""
      <h1>Hello live</h1>
      <button @click={run proxy.foo ()}>Foo</button>
      <button @click={run proxy.bar ("a", 1)}>Bar</button>
      <button @click={run proxy.baz ()}>Baz</button>
      <button @click={cancel}>Cancel</button>
      <hr>
      <ul>
        {Lit.ofList logs}
      </ul>
    """
