# Flaneur

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