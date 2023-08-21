namespace Flaneur.Remoting.IOS

open Foundation
open UIKit
open WebKit
open System
open System.Diagnostics
open System.IO


module private Request =

  let parameters queryString =
    if isNull queryString then Array.empty else
    let nameValues = System.Web.HttpUtility.ParseQueryString queryString
    nameValues.AllKeys
    |> Array.map (fun key -> key, nameValues.Get key)


module private Response =
  
  let headers nameValues =
    let headers = new NSMutableDictionary<NSString, NSString> ()
    for (name, value) in nameValues do
      headers.Add (NSString.op_Explicit name, NSString.op_Explicit value)
    headers

  let error (urlSchemeTask: IWKUrlSchemeTask) message =
    Debug.WriteLine message
    let headers = headers [
      "Cache-Control", "no-cache, max-age=0, must-revalidate, no-store"
      "Content-Type", "text/html"
    ]
    let data = NSData.FromString $"""
      <!doctype html>
      <html lang=en>
        <head>
          <meta charset=utf-8>
          <title>Flaneur can't start</title>
        </head>
        <body>
          <h1>Flaneur can't start</h1>
          <p>{message}</p>
        </body>
      </html>
      """
    let response = new NSHttpUrlResponse (urlSchemeTask.Request.Url, nativeint 500, "HTTP/1.1", headers)
    urlSchemeTask.DidReceiveResponse response
    urlSchemeTask.DidReceiveData data
    urlSchemeTask.DidFinish ()

type Handler<'Parameter, 'Result> = string -> 'Parameter array -> IObservable<'Result>

type DelegateSchemeHandler (handler : Handler<_, _>) =
  inherit NSObject ()

  let subscriptions = System.Runtime.CompilerServices.ConditionalWeakTable ()

  interface IWKUrlSchemeHandler with
    member _.StartUrlSchemeTask (webView, task) =
      // I think `DelegateSchemeHandler` gets created for each request...
      //assert isNull dispose

      // nope, reused so we need conditionalweak probs

      if task.Request.Url.Host <> "main" then
        Response.error task $"Delegate '{task.Request.Url.Host}' is not supported."
      else

      let headers = Response.headers [
        "Cache-Control", "no-cache, max-age=0, must-revalidate, no-store"
        "Content-Type", "text/plain"
        "Access-Control-Allow-Origin", "*"
      ]
      let response = new NSHttpUrlResponse (task.Request.Url, nativeint 200, "HTTP/1.1", headers)

      task.DidReceiveResponse response

      let parameters = Request.parameters task.Request.Url.Query
      let result = handler task.Request.Url.Path parameters
      
      let subscription = result.Subscribe({
        new IObserver<string> with
          member _.OnCompleted () =
            task.DidFinish ()

          member _.OnError error =
            task.DidReceiveData (NSData.FromString error.Message)
            task.DidFinish ()

          member _.OnNext value =
            task.DidReceiveData (NSData.FromString value)
      })

      subscriptions.Add (task, subscription)

    member _.StopUrlSchemeTask (webView, task) =
      match subscriptions.TryGetValue task with
      | true, subscription ->
        subscription.Dispose ()
      | false, _ ->
        ()

type BundleSchemeHandler () =
  inherit NSObject ()

  let toContentType extension =
    match extension with
    | ".html" -> Some "text/html"
    | ".js" -> Some "text/javascript"
    | _ -> None

  interface IWKUrlSchemeHandler with
    member _.StartUrlSchemeTask (wv, task) =
      if task.Request.Url.Host <> "main" then
        Response.error task $"Bundle '{task.Request.Url.Host}' is not supported."
      else

      let url =
        if task.Request.Url.PathExtension = ""
        then task.Request.Url.Path + "/index.html"
        else task.Request.Url.Path
      let name = Path.GetFileNameWithoutExtension url
      let ext = Path.GetExtension url
      let dir = Path.GetDirectoryName url

      let path = NSBundle.MainBundle.PathForResource (name, ext, dir)

      if isNull path then
        Response.error task $"File '{url}' cannot be found in bundle."
      else

      let file, error = NSData.FromFile (path, NSDataReadingOptions.Mapped)

      if not (isNull error) then
        Response.error task $"File '{url}' could not be loaded. {error.LocalizedDescription}"
      else

      match toContentType ext with
      | None ->
        Response.error task $"Unknown content type for file '{url}'."
      | Some ctype ->

      let headers = Response.headers [
        "Cache-Control", "no-cache, max-age=0, must-revalidate, no-store"
        "Content-Type", ctype
      ]
      let response = new NSHttpUrlResponse (task.Request.Url, nativeint 200, "HTTP/1.1", headers)
      task.DidReceiveResponse response
      task.DidReceiveData file
      task.DidFinish ()
        
    member _.StopUrlSchemeTask (webView, task) = ()

type WebAppViewController (launchUrl: NSUrl, handler) =
  inherit UIViewController()

  let cfg = new WKWebViewConfiguration()
  do cfg.SetUrlSchemeHandler(new BundleSchemeHandler(), urlScheme = "bundle")
  do cfg.SetUrlSchemeHandler(new DelegateSchemeHandler(handler), urlScheme = "delegate")
  let wv = new WKWebView (frame = CoreGraphics.CGRect.Null, configuration = cfg)
  do wv.Inspectable <- true
    
  override this.LoadView () = this.View <- wv
  
  override _.ViewDidLoad () =
      new NSUrlRequest (launchUrl)
      |> wv.LoadRequest
      |> ignore