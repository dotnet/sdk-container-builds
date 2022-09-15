# Developer Guide

This document contains getting-started resources for those that want to develop this tool.

## Using the prerelease package from this repository

The easiest way to use the prerelease package is to follow [these GitHub docs](https://docs.github.com/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry).

You'll want to use a nuget.config file to

* define a source for this repository
* define credentials (username and Personal Access Token) to use when accessing this source

Then, you can use `dotnet add package Microsoft.NET.Build.Containers -prerelease` to get the latest version.

You can also always clone this repository, run `dotnet build`, and use the newly-generated nupkg.

To use the package built from the CI pipeline:

* add the github package source to your nuget.config of choice
  * `dotnet nuget add source https://nuget.pkg.github.com/dotnet/index.json --name dotnet-org-github --username <your github username> --password <your github PAT with read:packages permission at minimum> --configfile <path to desired nuget config file>`
* add the package to a project
  * `dotnet add package Microsoft.NET.Build.Containers --prerelease` (or `--version <version number>`)

To use your locally built packages:

* Add a local nuget source:
  * `dotnet nuget add source C:\temp\packages -n local`
* Build the repo and note where the .nupkg is outputted:
  * `dotnet build -c Release`
  * ...
  * `Successfully created package 'C:\git\sdk-container-builds\Microsoft.NET.Build.Containers\bin\Release\Microsoft.NET.Build.Containers.0.2.0-alpha.14.gda83f2fee0.nupkg'.`
* Push the generated package to the local source:
  * `dotnet nuget push C:\git\sdk-container-builds\Microsoft.NET.Build.Containers\bin\Release\Microsoft.NET.Build.Containers.0.2.0-alpha.14.gda83f2fee0.nupkg -s local`
* In a test project, add the local package:
  * `dotnet add package Microsoft.NET.Build.Containers --version 0.2.0-alpha.14.gda83f2fee0`
* Publish the test project with `-p:PublishProfile=DefaultContainer` to create a container for your application:
  * `dotnet publish -c Release --os linux --arch x64 -p:PublishProfile=DefaultContainer`
* Run your test app to ensure it works
  * `docker run -it --rm -p 5010:80 myapp:1.0.0`

**NOTE**: Rebuilding and restoring local NuGet packages may cause versioning conflicts due to NuGet caching earlier iterations with the same version number. Be sure to delete the `microsoft.net.build.containers` folder in the NuGet cache in between building and restoring new changes to this repo. For example:
  * `rmdir -Force -Recurse ~\.nuget\packages\microsoft.net.build.containers`
  * `rm -rf ~/.nuget/packages/microsoft.net.build.containers`

## References

* [OCI Image Format spec](https://github.com/opencontainers/image-spec/blob/main/spec.md)
* [Docker Registry API docs](https://docs.docker.com/registry/spec/api/)
* [Setting up a local Docker registry](https://docs.docker.com/registry/)
