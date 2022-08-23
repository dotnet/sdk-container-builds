# .NET SDK Container Building Tools

This repo contains APIs and MSBuild Tasks for generating an OCI Container from a .NET project, as well as tests for the same.

Getting started with the library in an existing project is as easy as

```shell
dotnet add package Microsoft.NET.Build.Containers
dotnet publish --os linux --arch x64 -c Release -p:PublishProfile=DefaultContainer
```

You can learn more about the project from the project [Documentation](./docs).

[![.NET](https://github.com/dotnet/sdk-container-builds/actions/workflows/dotnet.yml/badge.svg)](https://github.com/dotnet/sdk-container-builds/actions/workflows/dotnet.yml)

## Prerequisites

In order to build the project you will need [.NET SDK 7.0.100, preview 7](https://dotnet.microsoft.com/download/dotnet/7.0) or greater installed.
From there, you can simply `dotnet build` the repository and be good to go!


## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for information on contributing to this project.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/)
to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

## License

This project is licensed with the [MIT license](LICENSE).

## .NET Foundation

sdk-container-builds is a [.NET Foundation project](https://dotnetfoundation.org/projects).

## Related Projects

You should take a look at these related projects:

- [Konet](https://github.com/lippertmarkus/konet)
- [`dotnet build-image`](https://github.com/tmds/build-image)
- [.NET SDK](https://github.com/dotnet/sdk)