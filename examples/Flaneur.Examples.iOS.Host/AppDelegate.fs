namespace Flaneur.Examples.iOS

open UIKit
open Foundation
open FSharp.Control


[<Register(nameof AppDelegate)>]
type AppDelegate() =
    inherit UIApplicationDelegate()
    
    let proxy (serviceName, args)=
        match serviceName, args with
        | "/foo", [||] ->
          asyncSeq { yield "1" }
          |> AsyncSeq.toObservable
        | "/fooWith", [|_; _|] -> 
          asyncSeq {
            yield "1"
            System.Threading.Thread.Sleep 1000
            yield "2"
          }
          |> AsyncSeq.toObservable
        | _ ->
        invalidOp "unknown service"
       
    override val Window = null with get, set

    override this.FinishedLaunching(application: UIApplication, launchOptions: NSDictionary) =
        // create a new window instance based on the screen size
        this.Window <- new UIWindow(UIScreen.MainScreen.Bounds)

        this.Window.RootViewController <- new WebAppViewController (proxy)

        this.Window.MakeKeyAndVisible()
        
        true