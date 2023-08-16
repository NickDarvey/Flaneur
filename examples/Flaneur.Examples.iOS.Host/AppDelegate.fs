namespace Flaneur.Examples.iOS

open UIKit
open Foundation
open FSharp.Control
open Flaneur.Remoting.IOS
open Thoth.Json.Net

// TODO: move this into share lib between host and app
type Animal = { Name: string; Age: int }
    

[<Register(nameof AppDelegate)>]
type AppDelegate() =
    inherit UIApplicationDelegate()

    let proxy =
      fun (serviceName,args) ->
        match serviceName, args with
        | "/foo", [||] ->
          asyncSeq { yield {| Value=1 |} }
          |> AsyncSeq.toObservable
          |> Observable.map (Encode.Auto.generateEncoder () >> fun x -> x.ToString())
        | "/fooWith", [|_; _|] -> 
          asyncSeq {
            yield { Name="Daisy"; Age=15 }
            do! Async.Sleep 1000
            yield { Name="Fluffle"; Age=9 }
          }
          |> AsyncSeq.toObservable
          |> Observable.map (Encode.Auto.generateEncoder () >> fun x -> x.ToString())
        | _ ->
        invalidOp "unknown service"
       
    override val Window = null with get, set

    override this.FinishedLaunching(application: UIApplication, launchOptions: NSDictionary) =
        // create a new window instance based on the screen size
        this.Window <- new UIWindow(UIScreen.MainScreen.Bounds)

        this.Window.RootViewController <- new WebAppViewController (proxy)

        this.Window.MakeKeyAndVisible()
        
        true