namespace Flaneur.Examples.iOS.Host

open UIKit
open Foundation
open FSharp.Data.LiteralProviders

open Flaneur
open Flaneur.Remoting

type private LaunchUrl = Env<"FLANEUR_URL">

module private Codec =
  module Result =
    let encode t (obj : obj) =
      let encoder = Thoth.Json.Net.Encode.Auto.LowLevel.generateEncoderCached t
      Thoth.Json.Net.Encode.toString 0 (encoder obj)

  module Arg =
    let decode t str =
      let decoder = Thoth.Json.Net.Decode.Auto.LowLevel.generateDecoderCached t

      match Thoth.Json.Net.Decode.fromString decoder str with
      | Ok result -> result
      | Error e ->
        raise <| exn $"Failed to decode response. {e}"

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
