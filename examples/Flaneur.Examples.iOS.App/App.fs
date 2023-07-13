module Lit.TodoMVC.App

open Lit

[<LitElement("my-app")>]
let MyApp() =
    let _ = LitElement.init(fun cfg ->
        cfg.useShadowDom <- false
    )
    html $"""<h1>Hello qewrqwer</h1>"""