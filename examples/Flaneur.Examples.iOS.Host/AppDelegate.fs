namespace Flaneur.Examples.iOS.Host

open UIKit
open Foundation
open Flaneur.Remoting.iOS
open FSharp.Data.LiteralProviders

type private LaunchUrl = Env<"FLANEUR_URL">

[<Register(nameof AppDelegate)>]
type AppDelegate() =
  inherit UIApplicationDelegate()

  let encodeResult t (obj : obj) =
    let encoder = Thoth.Json.Net.Encode.Auto.LowLevel.generateEncoderCached t
    Thoth.Json.Net.Encode.toString 0 (encoder obj)

  let decodeArg t arg =
    match t with
    | t when t = typeof<string> -> box arg
    | t when t = typeof<int> -> box <| System.Int32.Parse arg
    | t ->
      invalidOp $"Unsupported argument type '{t.Name}'."


  override val Window = null with get, set

  override this.FinishedLaunching
    (
      application : UIApplication,
      launchOptions : NSDictionary
    )
    =
    let url = new NSUrl (LaunchUrl.Value)
    let handler = Services.createExampleServiceHandler encodeResult decodeArg

    this.Window <- new UIWindow (UIScreen.MainScreen.Bounds)
    this.Window.RootViewController <- new WebAppViewController (url, handler)
    this.Window.MakeKeyAndVisible ()

    true
