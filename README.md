# Flaneur

## Launching

> **TODO**
> - If files have changed in the working directory, rebuild.

You can configure how the host launches your wapp by setting the _Flaneur launch URL_ variable.

For example, you can use a hot reload server while you're developing but use optimized bundled assets for a production release.

You can set the variable through the environment variable `FLANEUR_URL`. The default value is `bundle://main`.

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

If you have the `Flaneur.Launching.MSBuild` package installed, you can also set the variable using the MSBuild property `FlaneurLaunchUrl` or you can calculate the value through a command using the MSBuild property `FlaneurLaunchUrlCommand`.

**CLI**

```
dotnet build -t:Run -f net7.0-ios -p:_DeviceName=:v2:udid=MY_SPECIFIC_UDID` -p:FlaneurLaunchUrl=http://localhost:8080
```

**fsproj and package.json**

```xml
<PropertyGroup>
  <FlaneurLaunchUrlCommand>npm run -s print:start-url</FlaneurLaunchUrlCommand>
</PropertyGroup>
```

```json
{
  "private": true,
  "scripts": {
    "print:start-url": "node --print \"require('running-at')(8080).network\""
  },
  "devDependencies": {
    "running-at": "0.3.22"
  }
}
```



## Remoting

> ⚠️ Currently unused.

Code generation for Flaneur.

**TODO**:

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