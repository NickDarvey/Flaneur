namespace Flaneur.Examples.iOS

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

type FlaneurSchemeHandler (proxy: Proxy<string,string>) =
  inherit NSObject ()

  let mutable subscriptions = List.empty<IDisposable>

  let responseResource (urlSchemeTask: IWKUrlSchemeTask) (url:String)=
    let name = Path.GetFileNameWithoutExtension url
    let ext = Path.GetExtension url
    let dir = Path.GetDirectoryName url
    let path = NSBundle.MainBundle.PathForResource (name, ext, dir)
    let file, error = NSData.FromFile (path, NSDataReadingOptions.Mapped)
    if isNull error then
      let response = new NSUrlResponse(urlSchemeTask.Request.Url, "text/html",(nativeint file.Length), "iso-8859-1")
      urlSchemeTask.DidReceiveResponse response
      urlSchemeTask.DidReceiveData file
      urlSchemeTask.DidFinish ()
    else
      let error = error.LocalizedDescription
      let response = new NSUrlResponse(urlSchemeTask.Request.Url, "text/plain",(nativeint error.Length), "iso-8859-1")
      urlSchemeTask.DidReceiveResponse response
      urlSchemeTask.DidReceiveData (NSData.FromString error)
      urlSchemeTask.DidFinish ()
    ()

  interface IWKUrlSchemeHandler with
    member _.StartUrlSchemeTask (webView, urlSchemeTask) =
      let serviceName = urlSchemeTask.Request.Url.RelativePath
      if String.IsNullOrEmpty serviceName then
        responseResource urlSchemeTask "index.html"
      else
        let args =
          try
            let namedValue = System.Web.HttpUtility.ParseQueryString urlSchemeTask.Request.Url.Query
            namedValue.AllKeys
            |> Array.map (fun key -> namedValue.Get key)
          with
          | :? System.ArgumentNullException -> Array.empty

        let result = proxy(serviceName, args)
        let res = new NSHttpUrlResponse(urlSchemeTask.Request.Url, "text/plain", (nativeint 102400),"iso-8859-1")
        urlSchemeTask.DidReceiveResponse res  
        let obs = result.Subscribe (ServiceObserver urlSchemeTask)
        subscriptions <- subscriptions @ [obs]
      
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