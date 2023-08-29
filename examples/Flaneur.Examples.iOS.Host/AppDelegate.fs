namespace Flaneur.Examples.iOS.Host

open UIKit
open Foundation
open FSharp.Data.LiteralProviders

open Flaneur
open Flaneur.Remoting

type private LaunchUrl = Env<"FLANEUR_URL">

module Codec =
  module Result =
    let encode t (obj : obj) =
      let encoder = Thoth.Json.Net.Encode.Auto.LowLevel.generateEncoderCached t
      Thoth.Json.Net.Encode.toString 0 (encoder obj)

  module Arg =
    let decode t arg =
      match t with
      | t when t = typeof<string> -> box arg
      | t when t = typeof<int> -> box <| System.Int32.Parse arg
      | t ->
        invalidOp $"Unsupported argument type '{t.Name}'."


[<Register(nameof AppDelegate)>]
type AppDelegate() =
  inherit UIApplicationDelegate()


  override val Window = null with get, set

  override this.FinishedLaunching
    (
      application : UIApplication,
      launchOptions : NSDictionary
    )
    =
    let url = new NSUrl (LaunchUrl.Value)
    let handler = Services.createExampleServiceHandler Codec.Result.encode Codec.Arg.decode

    this.Window <- new UIWindow (UIScreen.MainScreen.Bounds)
    this.Window.RootViewController <- new WappViewController (url, WappViewController.configureWith handler)
    this.Window.MakeKeyAndVisible ()

    true
