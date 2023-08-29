# Flaneur

## Launching

### TODO: Launching 
1. Make the _Flanuer URL_ variable work out-of-the-box.

   Create a new [source code-only](https://medium.com/@attilah/source-code-only-nuget-packages-8f34a8fb4738) project, Flaneur.Launching, that makes the _Flaneur URL_ variable available at build time via an environment variable. (See also whatever `fable.core\4.0.0\contentFiles\any\netstandard2.0\RELEASE_NOTES.md` is doing to add itself, maybe it doesn't need to be a new project.)


1. Rebuild the host project if a file in `FlaneurWorkingDirectory` has changed.

   This might also be achieved by replacing the `FlaneurWorkingDirectory` with a Flaneur-tagged project reference (or a FlaneurReference?) so we can still find the right working directory and the default up-to-date-check of Visual Studio works.

1. Detect if the HTTP url (`FlaneurHttpUrlCommand`, `FlaneurHttpUrl`) is active and if so, don't run `FlaneurHttpCommand`.


### Launching with an environment variable

You can configure how the host launches your wapp at build time by setting a _Flaneur URL_ variable and using the [FSharp.Data.LiteralProviders](https://github.com/Tarmil/FSharp.Data.LiteralProviders) package.

**AppDelegate.fs**

```fsharp
type private LaunchUrl = Env<"FLANEUR_URL", "bundle://main">

[<Register(nameof AppDelegate)>]
type AppDelegate() =
    inherit UIApplicationDelegate()
       
    override val Window = null with get, set

    override this.FinishedLaunching(application: UIApplication, launchOptions: NSDictionary) =
        this.Window <- new UIWindow(UIScreen.MainScreen.Bounds)
        this.Window.RootViewController <- new WebAppViewController (new NSUrl(LaunchUrl.Value), handler)
        this.Window.MakeKeyAndVisible()
        true
```

For example, you can use a hot reload server while you're developing but use optimized bundled assets for a production release.

**CLI**

```
# TODO: start your wapp hot reload server, then
export FLANEUR_URL=http://localhost:8080
dotnet build -t:Run -f net7.0-ios -p:_DeviceName=:v2:udid=MY_SPECIFIC_UDID
```

**package.json**
```json
{
  "private": true,
  "scripts": {
    "build:wapp": "echo build your wapp",
    "build:host": "dotnet publish -f net7.0-ios -r ios-arm64 -c Release",
    "build": "npm-run-all build:wapp build:host",
    "start:wapp": "echo start your wapp hot reload server"
    "start:host": "cross-env FLANEUR_URL=http://localhost:8080 dotnet build -t:Run -f net7.0-ios -p:_DeviceName=:v2:udid=MY_SPECIFIC_UDID",
    "start": "npm-run-all --parallel start:*"
  },
  "devDependencies": {
    "cross-env": "7.0.3",
	"npm-run-all": "4.1.5"
  }
}

```


### Launching with Flaneur.Launching.MSBuild

If you have the `Flaneur.Launching.MSBuild` package installed, you can set the `FLANEUR_URL` environment variable using the MSBuild property `FlaneurUrl`.

**CLI**

```
dotnet build -t:Run -f net7.0-ios -p:_DeviceName=:v2:udid=MY_SPECIFIC_UDID` -p:FlaneurUrl=http://localhost:8080
```

You can also specify the behaviours for how Flaneur launches with _HTTP_ and _bundle_ so your wapp is built or started when you build or run the host in Visual Studio or via the dotnet CLI.

You can control whether your wapp is launched via _HTTP_ or _bundle_ with the property `FlaneurUseHttp`.

**fsproj and package.json**

```xml
<PropertyGroup>
    <FlaneurWorkingDirectory>../Flaneur.Examples.iOS.App</FlaneurWorkingDirectory>
    <FlaneurHttpUrlCommand>npm run -s print:start-url</FlaneurHttpUrlCommand>
    <FlaneurHttpCommand>npm run start</FlaneurHttpCommand>
    <FlaneurBundleAssetsCommand>npm run -s print:build-assets</FlaneurBundleAssetsCommand>
    <FlaneurBundleCommand>npm run build</FlaneurBundleCommand>

    <FlaneurUseHttp>false</FlaneurUseHttp>
</PropertyGroup>
```

```json
{
  "private": true,
  "scripts": {
    "print:start-url": "node --print \"require('running-at')(5173).network\"",
    "print:build-assets": "glob --nodir \"dist/**/*\"",
    "install": "dotnet tool restore",
    "build": "dotnet fable . -o bin/wwwroot --run vite build --outDir dist --emptyOutDir",
    "start": "dotnet fable watch . -s -o bin/wwwroot --run vite dev --host 0.0.0.0 --port 5173 --strictPort"
  },
  "devDependencies": {
    "glob": "10.3.3",
    "running-at": "0.3.22",
    "vite": "4.4.2"
  }
}
```



## Remoting

> ⚠️ Currently unused.

Code generation for Flaneur.

### TODO: Remoting

1. Reintegrate generators in example projects
	1. Add generator to `Flanuer.Examples.iOS.App.fsproj`.
	   ```xml
		<Compile Include="Services.g.fs" FlaneurRemotingSourceFile="Services.fs" FlaneurRemotingGenerators="FlaneurRemotingProxyGenerator" />
	   ```

	1. Add generator to `Flaneur.Examples.iOS.Host.fsproj`.
	   ```xml
	   <Compile Include="Services.g.fs" FlaneurRemotingSourceFile="../Flaneur.Examples.iOS.App/Services.fs" FlaneurRemotingGenerators="FlaneurRemotingHandlerGenerator" />
	   ```

    1. Uncomment `Flaneur.Remoting` references in `Directory.Build.props`.

	1. Add a project reference from `Flanuer.Examples.iOS.Host.fsproj` to `Flanuer.Examples.iOS.App.fsproj`.

	1. Attempt a release build for iOS.
	   
       Last time we had it integrated, the trimmer was trying to analyse Myriad and failing. If it does the same this time, investigate how to exclude the references from the build output.
	   - For NuGet packages it can be controlled with metadata.
	     
		 [Controlling dependency assets](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#controlling-dependency-assets)

	   - However, our example project uses project references.
	     
		 [Publish: PrivateAssets=All not honored on ProjectReference items](https://github.com/dotnet/sdk/issues/952)

1. Implement all the things. (Proxy generator, handler generator, etc.)