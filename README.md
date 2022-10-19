# .NET SDK Container Building Tools

This repo contains APIs and MSBuild Tasks for generating and testing an [OCI Container](https://opencontainers.org/) from a .NET project.

A basic start with the library in an existing project:

```shell
dotnet add package Microsoft.NET.Build.Containers
dotnet publish --os linux --arch x64 -c Release -p:PublishProfile=DefaultContainer
```

You can learn more about the project from the project [Documentation](./docs).

[![.NET](https://github.com/dotnet/sdk-container-builds/actions/workflows/dotnet.yml/badge.svg)](https://github.com/dotnet/sdk-container-builds/actions/workflows/dotnet.yml)

## Prerequisites

[.NET SDK 7.0.100, preview 7](https://dotnet.microsoft.com/download/dotnet/7.0) or later 

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for information on contributing to this project.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/)
to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

## License

This project is licensed with the [MIT license](LICENSE).

## .NET Foundation

sdk-container-builds is a [.NET Foundation project](https://dotnetfoundation.org/projects).

## Related Projects

- [.NET docker-tools](https://github.com/dotnet/docker-tools)
- [Konet](https://github.com/lippertmarkus/konet)
- [`dotnet build-image`](https://github.com/tmds/build-image)
- [.NET SDK](https://github.com/dotnet/sdk)
