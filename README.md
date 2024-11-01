# .NET SDK Container Building Tools

This project consists of APIs and MSBuild Tasks for generating and testing an [OCI Container](https://opencontainers.org/) from a .NET project.

A basic start with tooling 

- for existing web project (the package is part of `Microsoft.NET.SDK.Web):

```shell
dotnet publish --os linux --arch x64 -t:PublishContainer
```

- for existing non-web project:

```shell
dotnet add package Microsoft.NET.Build.Containers
dotnet publish --os linux --arch x64 -c Release /t:PublishContainer
```

You can learn more about the project from the project [Documentation](./docs).

## Prerequisites

[.NET 7 SDK](https://dotnet.microsoft.com/download/dotnet/7.0) or later 

## Contributing

This repo only contains documentation and issues for the project.

The source code was moved to [dotnet/sdk](https://github.com/dotnet/sdk/tree/main/src/Containers) repo.
For easiness, please use [containers.slnf](https://github.com/dotnet/sdk/blob/main/containers.slnf) filter in case the intention is only to build and debug containers source code.

If you would like to contribute by creating a pull request, please refer to [dotnet/sdk contributing guide](https://github.com/dotnet/sdk#how-do-i-engage-and-contribute). 
Ideally, prior starting the effort find a corresponding issue in [this repo](https://github.com/dotnet/sdk-container-builds/issues) and let us and others know in a comment. If you plan to address the problem that is not reflected in any issue, please create one. Consider helping us triaging the pull request by adding 'Area-Containers' repo.

Development documentation is available [here](./docs/DevelopmentDocumentation.md)

## License

This project is licensed with the [MIT license](LICENSE).

## .NET Foundation

sdk-container-builds is a [.NET Foundation project](https://dotnetfoundation.org/projects).

## Related Projects

- [.NET docker-tools](https://github.com/dotnet/docker-tools)
- [Konet](https://github.com/lippertmarkus/konet)
- [`dotnet build-image`](https://github.com/tmds/build-image)
- [.NET SDK](https://github.com/dotnet/sdk)
