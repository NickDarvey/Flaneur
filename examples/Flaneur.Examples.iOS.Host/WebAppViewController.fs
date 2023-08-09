namespace Flaneur.Examples.iOS

open Foundation
open UIKit
open WebKit
open System
open System.IO
open FSharp.Data.LiteralProviders
open FSharp.Control


type ServiceObserver (urlSchemeTask: IWKUrlSchemeTask) =
  interface IObserver<string> with
    member _.OnCompleted () =
      urlSchemeTask.DidFinish ()

    member _.OnError error =
      urlSchemeTask.DidReceiveData (NSData.FromString error.Message)
      urlSchemeTask.DidFinish ()

    member _.OnNext value =
      urlSchemeTask.DidReceiveData (NSData.FromString value)

type ServiceHandler() =
  inherit NSObject ()

  let mutable subscriptions = List.empty<IDisposable>
  
  let handle serviceName args =
    match serviceName, args with
    | "/foo", None ->
      asyncSeq { yield "1" }
      |> AsyncSeq.toObservable
    | "/fooWith", Some pr -> 
      for (k, v) in pr do
        printfn $"fooWith key= {k} ; value = {v}"
      asyncSeq {
        yield "1"
        System.Threading.Thread.Sleep 1000
        yield "2"
      }
      |> AsyncSeq.toObservable
    | _ ->
    invalidOp "unknown service"

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
      let response = new NSUrlResponse(urlSchemeTask.Request.Url, "text/html",(nativeint error.Length), "iso-8859-1")
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
            |> Array.map (fun key -> key, namedValue.Get key)
            |> Some 
          with
          | :? System.ArgumentNullException -> None

        let result = handle serviceName args
        let res = new NSHttpUrlResponse(urlSchemeTask.Request.Url, "text/plain", (nativeint 102400),"iso-8859-1")
        urlSchemeTask.DidReceiveResponse res  
        let obs = result.Subscribe (ServiceObserver urlSchemeTask)
        subscriptions <- subscriptions @ [obs]

      
    member _.StopUrlSchemeTask (webView, urlSchemeTask) =
      for subscription in subscriptions do
        subscription.Dispose ()
      
type private LaunchUrl = Env<"FLANEUR_LAUNCH_URL", "flaneur://app">

type WebAppViewController () =
  inherit UIViewController()
  let cfg = new WKWebViewConfiguration()
  do cfg.SetUrlSchemeHandler(new ServiceHandler (), urlScheme = "flaneur")
  let wv = new WKWebView (frame = CoreGraphics.CGRect.Null, configuration = cfg)
  do wv.Inspectable <- true
  
  override this.LoadView() =
    this.View <- wv

  override _.ViewDidLoad () =
    new NSMutableUrlRequest (new NSUrl (LaunchUrl.Value))
    |> wv.LoadRequest
    |> ignore