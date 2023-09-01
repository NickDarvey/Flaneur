namespace Flaneur.Remoting

open System
open System.Diagnostics

open Foundation
open WebKit

module private Request =
  let parameters queryString =
    if isNull queryString then
      Array.empty
    else
      let nameValues = System.Web.HttpUtility.ParseQueryString queryString
      nameValues.AllKeys
      // Remove remoting protocol-specific parameters.
      |> Array.filter (fun key -> not <| key.StartsWith '$')
      |> Array.map (fun key -> nameValues.Get key)

module private Response =
  let headers nameValues =
    let headers = new NSMutableDictionary<NSString, NSString> ()

    for (name, value) in nameValues do
      headers.Add (NSString.op_Explicit name, NSString.op_Explicit value)

    headers

type RemotingSchemeHandler(handler : Handler<_, _>) =
  inherit NSObject()

  let subscriptions = System.Runtime.CompilerServices.ConditionalWeakTable ()

  interface IWKUrlSchemeHandler with
    member _.StartUrlSchemeTask (webView, task) =
      Debug.WriteLine $"Starting request {task.Request.Url.ToString ()}"

      if task.Request.Url.Host <> "main" then
        invalidOp
          $"Delegate '{task.Request.Url.Host}' is not supported."
      else

      let headers =
        Response.headers [
          "Cache-Control", "no-cache, max-age=0, must-revalidate, no-store"
          "Content-Type", "text/plain"
          "Access-Control-Allow-Origin", "*"
        ]

      let response =
        new NSHttpUrlResponse (
          task.Request.Url,
          nativeint 200,
          "HTTP/1.1",
          headers
        )

      task.DidReceiveResponse response

      let parameters = Request.parameters task.Request.Url.Query
      let result = handler task.Request.Url.Path parameters

      let subscription =
        result.Subscribe (
          { new IObserver<string> with
              member _.OnCompleted () =
                Debug.WriteLine $"Completed request {task.Request.Url.ToString ()}"
                task.DidFinish ()

              member _.OnError error =
                Debug.WriteLine $"Error occured in result stream, will return to caller. (type = {error.GetType().Name}; message = '{error.Message}')"
                task.DidReceiveData (NSData.FromString error.Message)
                task.DidFinish ()

              member _.OnNext value =
                task.DidReceiveData (NSData.FromString value)
          }
        )

      subscriptions.Add (task.Request, subscription)

    member _.StopUrlSchemeTask (webView, task) =
      Debug.WriteLine $"Stopping request {task.Request.Url.ToString ()}"
      match subscriptions.TryGetValue task.Request with
      | true, subscription ->
        subscription.Dispose ()
      | false, _ ->
        ()

module WappViewController =
  let configureWith handler (cfg : WKWebViewConfiguration) =
    do
      cfg.SetUrlSchemeHandler (
        new RemotingSchemeHandler (handler),
        urlScheme = "remoting"
      )
