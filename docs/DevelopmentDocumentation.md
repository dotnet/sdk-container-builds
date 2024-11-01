# Development guide

## Implementation details

The source code for the project is stored in [dotnet/sdk](https://github.com/dotnet/sdk/tree/main/src/Containers) repo.

It contains the following projects:
|Project|Description|
|---|---|
|[`Microsoft.NET.Build.Containers`](https://github.com/dotnet/sdk/tree/main/src/Containers/Microsoft.NET.Build.Containers)|The project contains MSBuild tasks for publishing .NET containers.|
|[`containerize`](https://github.com/dotnet/sdk/tree/main/src/Containers/containerize)|.NET CLI app used for container publishing from .NET Framework environment (as Visual Studio).|
|[`packaging`](https://github.com/dotnet/sdk/tree/main/src/Containers/packaging)|The project contains MSBuild targets and used for packaging the project to NuGet package.|

`Microsoft.NET.Build.Containers` contains the following tasks:
- [`CreateNewImage`](https://github.com/dotnet/sdk/blob/main/src/Containers/Microsoft.NET.Build.Containers/Tasks/CreateNewImage.cs) - creates the container image (note different implementation for .NET Framework).
- [`ComputeDotnetBaseImageTag`](https://github.com/dotnet/sdk/blob/main/src/Containers/Microsoft.NET.Build.Containers/Tasks/ComputeDotnetBaseImageTag.cs) - computes the base image Tag for a Microsoft-authored container image based on the tagging scheme from various SDK versions.
- [`ParseContainerProperties`](https://github.com/dotnet/sdk/blob/main/src/Containers/Microsoft.NET.Build.Containers/Tasks/ParseContainerProperties.cs) - parses certain container properties: base image name, container registry, container image name, image tags.

To simplify the work in dotnet/sdk repo, there is the [containers](https://github.com/dotnet/sdk/blob/main/containers.slnf) solution filter. It loads all the projects needed. 
Please note the [details](https://github.com/dotnet/sdk/blob/main/documentation/project-docs/developer-guide.md) for working with dotnet/sdk repo:
- the project should be build using command-line build scripts (build.cmd/build.sh)
- before opening solution/solution filter in Visual Studio `artifacts\sdk-build-env.bat` and `eng\dogfood.cmd` should be run.

### Insertion to `Microsoft.NET.Sdk.Publish`

The container targets are inserted to `Microsoft.NET.Sdk.Publish`. 
Main insertions points are:
- [`Microsoft.NET.Sdk.Publish.props`](TBD)
- [`Microsoft.NET.Sdk.Publish.targets`](TBD)

Required artifacts are [included](TBD) to .NET SDK.

### Error messages

All error messages should have a dedicated code starting from `CONTAINER`. 
The output from `containerize` should comply with [MSBuild canonical format](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-diagnostic-format-for-tasks?view=vs-2022).
All non-debug output should be localized.

## Use cases

### .NET SDK / `dotnet publish`

- for web project:
```shell
>dotnet publish --os linux --arch x64 -t:PublishContainer
...
Pushed container '<your app name>:<your app version>' to registry 'docker://'
...
```

- for non-web project:
```shell
>dotnet publish --os linux --arch x64 -t:PublishContainer
...
Pushed container '<your app name>:<your app version>' to registry 'docker://'
...
```

### Visual Studio

It is possible to publish both web project and non-web project using `Publish` dialog.
See the details in [the article](https://learn.microsoft.com/en-us/visualstudio/containers/deploy-containerized?view=vs-2022).

Note: Visual Studio is using .NET Framework version of .NET containers MSBuild task.

## Automated and manual tests

### Automated tests 
The following automated tests are available:
- [`containerize.UnitTests`](https://github.com/dotnet/sdk/tree/main/src/Tests/containerize.UnitTests) - contains unit tests for `containerize`.
- [`Microsoft.NET.Build.Containers.UnitTests`](https://github.com/dotnet/sdk/tree/main/src/Tests/Microsoft.NET.Build.Containers.UnitTests) - contains unit tests for `Microsoft.NET.Build.Containers`.
- [`Microsoft.NET.Build.Containers.IntegrationTests`](https://github.com/dotnet/sdk/tree/main/src/Tests/Microsoft.NET.Build.Containers.IntegrationTests) - contains integration tests for `Microsoft.NET.Build.Containers`. Majority of integration tests requires `docker` to be installed. On CI they run on `Ubuntu` only. It is possible to run those tests in Windows environment if Docker Desktop is installed. 

Note: if `docker` is not detected, majority of integration tests will be skipped due to custom [fact and theory attributes](https://github.com/dotnet/sdk/blob/main/src/Tests/Microsoft.NET.Build.Containers.UnitTests/DockerDaemonAvailableUtils.cs).

All the integration tests are run for `linux` containers. The integration tests are covering only .NET SDK use cases.

#### [sdk-container-demo](https://github.com/baronfel/sdk-container-demo)

This is the testing project run by @baronfel. 
It runs full end-to-end tests to well-known registries. The tests are covering only .NET SDK use case for web application.
The tests are run once a day.

### Manual tests

The following scenarios are not covered by automated tests and should be checked manually:
- publishing of web project in Visual Studio
- publishing of non-web project in Visual Studio
- publishing windows container (details TBD)

To test new build of dotnet/sdk repo in VS, ensure that `eng\dogfood.cmd` is run before. This script configures the environment to use `dotnet` from build artifacts folder.

## Release cadence

Starting .NET SDK 7.0.300, the artifacts are released together with .NET SDK.
`Microsoft.NET.Build.Containers` NuGet package is released to nuget.org at the day of .NET SDK release. Versioning matches .NET SDK version.

Note: `Microsoft.NET.Build.Containers` NuGet package will be deprecated soon, potentially with .NET 8. 
It is planned that functionality is fully available from .NET SDK since then. 
For web projects, package reference to `Microsoft.NET.Build.Containers` is already not needed as targets are available from `Microsoft.NET.Sdk.Publish`. In case the package reference is added, the warning appears during publishing (starting .NET SDK 7.0.400).
For non-web projects, as of .NET SDK 7.0.300 package reference to NuGet package is still needed, but we are working towards simplifying that.

## Short and long term vision for the project
See https://github.com/dotnet/sdk-container-builds/issues/428 for details.

## Troubleshooting

### Troubleshooting issues in VS.
MSBuild binlog may be used to see if `PublishContainer` target was run.
In case it was run, likely `containerize` app was launched by `CreateNewImage` task.
The command that was run is logged in Build Output window. 

To troubleshoot the issues, you may want to run this command from CLI - it provides more output. 
If needed it can be also debugged.

## Resources
* [Tutorial: Containerize a .NET app with dotnet publish](https://learn.microsoft.com/en-us/dotnet/core/docker/publish-as-container)
* [Release notes](https://github.com/dotnet/sdk/tree/main/src/Containers/docs/ReleaseNotes)
* [OCI Image Format spec](https://github.com/opencontainers/image-spec/blob/main/spec.md)
* [Docker Registry API docs](https://docs.docker.com/registry/spec/api/)
* [Setting up a local Docker registry](https://docs.docker.com/registry/)