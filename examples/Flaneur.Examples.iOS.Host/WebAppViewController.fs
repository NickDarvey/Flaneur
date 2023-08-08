namespace Flaneur.Examples.iOS

open Foundation
open UIKit
open WebKit
open System
open System.IO
open FSharp.Data.LiteralProviders

type BundleWKUrlSchemeHandler () =
  inherit NSObject ()

  interface IWKUrlSchemeHandler with
    member _.StartUrlSchemeTask (webView, urlSchemeTask) =

      let url = urlSchemeTask.Request.Url.RelativePath
      let url = if String.IsNullOrEmpty url then "index.html" else url
      let name = Path.GetFileNameWithoutExtension url
      let ext = Path.GetExtension url
      let dir = Path.GetDirectoryName url
      let path = NSBundle.MainBundle.PathForResource (name, ext, dir)
      let file, error = NSData.FromFile (path, NSDataReadingOptions.Mapped)
      if isNull error then
        let headers = new NSMutableDictionary<NSString, NSString> ()
        headers.Add (NSString.op_Explicit "Content-Length", NSString.op_Explicit $"{file.Length}")
        headers.Add (NSString.op_Explicit "Content-Type", NSString.op_Explicit "text/html")
        headers.Add (NSString.op_Explicit "Cache-Control", NSString.op_Explicit "no-cache, max-age=0, must-revalidate, no-store")
        let response = new NSHttpUrlResponse (urlSchemeTask.Request.Url, (nativeint 200), "HTTP/1.1", headers)
        urlSchemeTask.DidReceiveResponse response
        urlSchemeTask.DidReceiveData file
        urlSchemeTask.DidFinish ()
      else
        let error = error.LocalizedDescription
        let headers = new NSMutableDictionary<NSString, NSString> ()
        headers.Add (NSString.op_Explicit "Content-Length", NSString.op_Explicit $"{error.Length}")
        headers.Add (NSString.op_Explicit "Content-Type", NSString.op_Explicit "text/plain")
        headers.Add (NSString.op_Explicit "Cache-Control", NSString.op_Explicit "no-cache, max-age=0, must-revalidate, no-store")
        let response = new NSHttpUrlResponse (urlSchemeTask.Request.Url, (nativeint 404), "HTTP/1.1", headers)
        urlSchemeTask.DidReceiveResponse response
        urlSchemeTask.DidReceiveData (NSData.FromString error)
        urlSchemeTask.DidFinish ()
      ()
    member _.StopUrlSchemeTask (webView, urlSchemeTask) =
      let asdf = urlSchemeTask.Request.Url
      printfn $"stop {asdf}"
      ()


type ServiceHandler () =
  inherit NSObject ()

  let toParams (url: NSUrl) = 
    try
      let namedValue = System.Web.HttpUtility.ParseQueryString url.Query
      namedValue.AllKeys
      |> Array.map (fun key -> key, namedValue.Get key)
      |> Some 
    with
    | :? System.ArgumentNullException -> None

  let toServiceName (url: NSUrl) = url.RelativePath

  let handle serviceName args =
    match serviceName, args with
    | "/foo", None -> "unit"
    | "/fooWith", Some pr -> 
      for (k, v) in pr do
        printfn $"fooWith key= {k} ; value = {v}"
      "bar"
    | _ ->
      invalidOp "unknown service"

  interface IWKUrlSchemeHandler with
    member _.StartUrlSchemeTask (webView, urlSchemeTask) =
      let url = urlSchemeTask.Request.Url 
      printfn $"ServiceHandler.StartUrlSchemeTask with url {url}"
      if isNull url then () else

      let serviceName = toServiceName url
      let args = toParams url
      printfn $"{urlSchemeTask.Request.Url.Query}"
      printfn $"start {serviceName}"

      let result = handle serviceName args

      let headers = new NSMutableDictionary<NSString, NSString> ()
      headers.Add (NSString.op_Explicit "Content-Type", NSString.op_Explicit "text/plain")
      headers.Add (NSString.op_Explicit "Cache-Control", NSString.op_Explicit "no-cache, max-age=0, must-revalidate, no-store")
      let res = new NSHttpUrlResponse(url, (nativeint 200), "HTTP/1.1", headers)
      
      urlSchemeTask.DidReceiveResponse res
      
      urlSchemeTask.DidReceiveData (NSData.FromString result)
      urlSchemeTask.DidFinish ()
      ()

    member _.StopUrlSchemeTask (webView, urlSchemeTask) =
      let asdf = urlSchemeTask.Request.Url
      printfn $"stop {asdf}"
      ()

type private LaunchUrl = Env<"FLANEUR_LAUNCH_URL", "bundle://">

type WebAppViewController () =
  inherit UIViewController()

  let cfg = new WKWebViewConfiguration()
  do cfg.SetUrlSchemeHandler(new BundleWKUrlSchemeHandler (), urlScheme = "bundle")
  do cfg.SetUrlSchemeHandler(new ServiceHandler (), urlScheme = "flaneur")
  let wv = new WKWebView (frame = CoreGraphics.CGRect.Null, configuration = cfg)

  override this.LoadView() =
    this.View <- wv

  override this.ViewDidLoad () =
    let req = new NSUrlRequest (new NSUrl (LaunchUrl.Value))
    let nav = wv.LoadRequest req
    //let file = NSBundle.MainBundle.PathForResource ("index", "html")
    //let content = System.IO.File.ReadAllText file
    //let res = wv.LoadHtmlString (content, null);

    ()