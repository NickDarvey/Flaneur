namespace Flaneur.Examples.iOS

open UIKit
open Foundation

[<Register(nameof AppDelegate)>]
type AppDelegate() =
    inherit UIApplicationDelegate()
       
    override val Window = null with get, set

    override this.FinishedLaunching(application: UIApplication, launchOptions: NSDictionary) =
        // create a new window instance based on the screen size
        this.Window <- new UIWindow(UIScreen.MainScreen.Bounds)

        this.Window.RootViewController <- new WebAppViewController ()

        this.Window.MakeKeyAndVisible()
        
        true