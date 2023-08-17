namespace Flaneur.Remoting.IOS

open Foundation
open UIKit
open WebKit
open System
open System.IO
open FSharp.Data.LiteralProviders

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

  interface IWKUrlSchemeHandler with
    member _.StartUrlSchemeTask (webView, urlSchemeTask) =
      let serviceName = urlSchemeTask.Request.Url.RelativePath
      if String.IsNullOrEmpty serviceName then
        handleAssetRequest urlSchemeTask "index.html"
      else handleServiceRequest urlSchemeTask serviceName
        
    member _.StopUrlSchemeTask (webView, urlSchemeTask) =
      for subscription in subscriptions do
        subscription.Dispose ()

type private LaunchUrl = Env<"FLANEUR_LAUNCH_URL", "flaneur://app">

type WebAppViewController (proxy) =
  inherit UIViewController()
  let cfg = new WKWebViewConfiguration()
  do cfg.SetUrlSchemeHandler(new FlaneurSchemeHandler(proxy), urlScheme = "flaneur")
  let wv = new WKWebView (frame = CoreGraphics.CGRect.Null, configuration = cfg)
  do wv.Inspectable <- true
    
  override this.LoadView () = this.View <- wv
  
  override _.ViewDidLoad () =
      new NSMutableUrlRequest (new NSUrl (LaunchUrl.Value))
      |> wv.LoadRequest
      |> ignore