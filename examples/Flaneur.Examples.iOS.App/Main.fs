module Flaneur.Examples.iOS.App.Main

open Lit
open Browser
open Services

[<LitElement("my-app")>]
let MyApp() = 
    let _ = LitElement.init(fun cfg ->
        cfg.useShadowDom <- false
    )
    let fooProxy = FooProxy "flaneur://app"
    let runFoo () =
      let obs = fooProxy.Foo ()
      obs.Subscribe(fun e -> console.log($"foo: {e}"))

    let runFooWith () =
      let obs = fooProxy.FooWith ("a",1)
      obs.Subscribe(fun e -> console.log($"foowith: {e}"))

    html $"""
      <h1>Hello qewrqwer</h1>
      <button @click={runFoo}>Foo</button>
      <button @click={runFooWith}>FooWith</button>
    """