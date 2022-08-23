# Developer Guide

This document contains getting-started resources for those that want to develop this tool.

## Using the prerelease package from this repository

The easiest way to use the prerelease package is to follow [these GitHub docs](https://docs.github.com/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry).

You'll want to use a nuget.config file to

* define a source for this repository
* define credentials (username and Personal Access Token) to use when accessing this source

Then, you can use `dotnet add package Microsoft.NET.Build.Containers -prerelease` to get the latest version.

You can also always clone this repository, run `dotnet build`, and use the newly-generated nupkg.

## References

* [OCI Image Format spec](https://github.com/opencontainers/image-spec/blob/main/spec.md)
* [Docker Registry API docs](https://docs.docker.com/registry/spec/api/)
* [Setting up a local Docker registry](https://docs.docker.com/registry/)