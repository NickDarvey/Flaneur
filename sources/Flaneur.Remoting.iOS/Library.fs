namespace Flaneur.Remoting.IOS

open Foundation
open UIKit
open WebKit
open System
open System.Diagnostics
open System.IO

type Proxy<'Parameter, 'Result> = string * ('Parameter array) -> IObservable<'Result>

type ServiceObserver (urlSchemeTask: IWKUrlSchemeTask) =
  interface IObserver<string> with
    member _.OnCompleted () =
      urlSchemeTask.DidFinish ()

    member _.OnError error =
      urlSchemeTask.DidReceiveData (NSData.FromString error.Message)
      urlSchemeTask.DidFinish ()

    member _.OnNext value =
      urlSchemeTask.DidReceiveData (NSData.FromString value)

type FlaneurSchemeHandler (proxy: Proxy<_, _>) =
  inherit NSObject ()

  let mutable subscriptions = List.empty<IDisposable>

  let toHeaders nameValues =
    let headers = new NSMutableDictionary<NSString, NSString> ()
    for (name, value) in nameValues do
      headers.Add (NSString.op_Explicit name, NSString.op_Explicit value)
    headers

  let toContentType extension =
    match extension with
    | ".html" -> Some "text/html"
    | ".js" -> Some "text/javascript"
    | _ -> None

  let returnErrorResponse (urlSchemeTask: IWKUrlSchemeTask) message =
    Debug.WriteLine message
    let headers = toHeaders [
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

  let handleAssetRequest (urlSchemeTask: IWKUrlSchemeTask) (url:String)=
    let name = Path.GetFileNameWithoutExtension url
    let ext = Path.GetExtension url
    let dir = Path.GetDirectoryName url
    let path = NSBundle.MainBundle.PathForResource (name, ext, dir)
    let file, error = NSData.FromFile (path, NSDataReadingOptions.Mapped)
    
    let contentType, statusCode, data =
      if isNull error then "text/html", (nativeint 200), file
      else "text/plain", (nativeint 500), (NSData.FromString error.LocalizedDescription)

    let headers = new NSMutableDictionary<NSString, NSString> ()
    headers.Add (NSString.op_Explicit "Access-Control-Allow-Origin", NSString.op_Explicit "*")
    headers.Add (NSString.op_Explicit "Cache-Control", NSString.op_Explicit "no-cache, max-age=0, must-revalidate, no-store")
    headers.Add (NSString.op_Explicit "Content-Type", NSString.op_Explicit contentType)
    let response = new NSHttpUrlResponse (urlSchemeTask.Request.Url, statusCode, "HTTP/1.1", headers)
    urlSchemeTask.DidReceiveResponse response
    urlSchemeTask.DidReceiveData data
    urlSchemeTask.DidFinish ()

  let handleServiceRequest (urlSchemeTask: IWKUrlSchemeTask) (serviceName:String) = 
    let args =
      try
        let namedValue = System.Web.HttpUtility.ParseQueryString urlSchemeTask.Request.Url.Query
        namedValue.AllKeys
        |> Array.map (fun key -> namedValue.Get key)
      with
      | :? System.ArgumentNullException -> Array.empty


    let result = proxy(serviceName, args)
    let headers = new NSMutableDictionary<NSString, NSString> ()
    headers.Add (NSString.op_Explicit "Access-Control-Allow-Origin", NSString.op_Explicit "*")
    headers.Add (NSString.op_Explicit "Cache-Control", NSString.op_Explicit "no-cache, max-age=0, must-revalidate, no-store")
    headers.Add (NSString.op_Explicit "Content-Type", NSString.op_Explicit "application/json")
    let res = new NSHttpUrlResponse (urlSchemeTask.Request.Url, (nativeint 200), "HTTP/1.1", headers)
    
    urlSchemeTask.DidReceiveResponse res  
    let obs = result.Subscribe (ServiceObserver urlSchemeTask)
    subscriptions <- subscriptions @ [obs]

  let returnAssetsResponse (urlSchemeTask: IWKUrlSchemeTask) =
    let url =
      if urlSchemeTask.Request.Url.PathExtension = ""
      then urlSchemeTask.Request.Url.Path + "/index.html"
      else urlSchemeTask.Request.Url.Path
    let name = Path.GetFileNameWithoutExtension url
    let ext = Path.GetExtension url
    let dir = Path.GetDirectoryName url
    let path = NSBundle.MainBundle.PathForResource (name, ext, dir)

    if isNull path then
      returnErrorResponse urlSchemeTask $"File '{url}' cannot be found in bundle."
    else

    let file, error = NSData.FromFile (path, NSDataReadingOptions.Mapped)

    if not (isNull error) then
      returnErrorResponse urlSchemeTask $"File '{url}' could not be loaded. {error.LocalizedDescription}"
    else

    match toContentType ext with
    | None ->
      returnErrorResponse urlSchemeTask $"Unknown content type for file '{url}'."
    | Some ctype ->

    let headers = toHeaders [
      "Cache-Control", "no-cache, max-age=0, must-revalidate, no-store"
      "Content-Type", ctype
    ]
    let response = new NSHttpUrlResponse (urlSchemeTask.Request.Url, nativeint 200, "HTTP/1.1", headers)
    urlSchemeTask.DidReceiveResponse response
    urlSchemeTask.DidReceiveData file
    urlSchemeTask.DidFinish ()

  interface IWKUrlSchemeHandler with
    member _.StartUrlSchemeTask (webView, urlSchemeTask) =
      match urlSchemeTask.Request.Url.Host with
      | "bundle" ->
        returnAssetsResponse urlSchemeTask
        ()
      | "handler" ->
        let serviceName = urlSchemeTask.Request.Url.RelativePath
        if String.IsNullOrEmpty serviceName then
          handleAssetRequest urlSchemeTask "index.html"
        else handleServiceRequest urlSchemeTask serviceName
      | host ->
        returnErrorResponse urlSchemeTask $"\
          Unsupported host '{host}'. The supported hosts are 'bundle' and 'handler'."
        
    member _.StopUrlSchemeTask (webView, urlSchemeTask) =
      for subscription in subscriptions do
        subscription.Dispose ()

type WebAppViewController (launchUrl: NSUrl, proxy) =
  inherit UIViewController()

  let cfg = new WKWebViewConfiguration()
  do cfg.SetUrlSchemeHandler(new FlaneurSchemeHandler(proxy), urlScheme = "flaneur")
  let wv = new WKWebView (frame = CoreGraphics.CGRect.Null, configuration = cfg)
  do wv.Inspectable <- true
    
  override this.LoadView () = this.View <- wv
  
  override _.ViewDidLoad () =
      new NSUrlRequest (launchUrl)
      |> wv.LoadRequest
      |> ignore