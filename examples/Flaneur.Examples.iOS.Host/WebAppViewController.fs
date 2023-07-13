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
        let response = new NSHttpUrlResponse (urlSchemeTask.Request.Url, 200, "HTTP/1.1", headers)
        urlSchemeTask.DidReceiveResponse response
        urlSchemeTask.DidReceiveData file
        urlSchemeTask.DidFinish ()
      else
        let error = error.LocalizedDescription
        let headers = new NSMutableDictionary<NSString, NSString> ()
        headers.Add (NSString.op_Explicit "Content-Length", NSString.op_Explicit $"{error.Length}")
        headers.Add (NSString.op_Explicit "Content-Type", NSString.op_Explicit "text/plain")
        headers.Add (NSString.op_Explicit "Cache-Control", NSString.op_Explicit "no-cache, max-age=0, must-revalidate, no-store")
        let response = new NSHttpUrlResponse (urlSchemeTask.Request.Url, 404, "HTTP/1.1", headers)
        urlSchemeTask.DidReceiveResponse response
        urlSchemeTask.DidReceiveData (NSData.FromString error)
        urlSchemeTask.DidFinish ()
      ()
    member _.StopUrlSchemeTask (webView, urlSchemeTask) =
      let asdf = urlSchemeTask.Request.Url
      printfn $"stop {asdf}"
      ()


type Bloop () =
  inherit NSObject ()

  interface IWKUrlSchemeHandler with
    member _.StartUrlSchemeTask (webView, urlSchemeTask) =
      let asdf = urlSchemeTask.Request.Url
      printfn $"start {asdf}"
      //urlSchemeTask.Request.Url
      ()
    member _.StopUrlSchemeTask (webView, urlSchemeTask) =
      let asdf = urlSchemeTask.Request.Url
      printfn $"stop {asdf}"
      ()

type private LaunchUrl = Env<"FLANEUR_LAUNCH_URL", "bundle://">

type WebAppViewController () =
  inherit UIViewController()

  let cfg = new WKWebViewConfiguration()
  //do cfg.SetUrlSchemeHandler(new Bloop (), urlScheme = "flaneur")
  do cfg.SetUrlSchemeHandler(new BundleWKUrlSchemeHandler (), urlScheme = "bundle")
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