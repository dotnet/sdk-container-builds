# Customizing your container

You can control many aspects of the generated container through MSBuild properties. In general, if you could use a command in a Dockerfile to set some configuration, you can do the same via MSBuild.

> **Note**
> The only exception to this is `RUN` commands - due to the way we build containers, those cannot be emulated. If you need this functionality, you will need to use a Dockerfile to build your container images.

## Choosing a base image

These properties help the tools determine which image to build your container on top of.

### ContainerBaseImage

This property controls the image used as the basis for your image. By default, we will infer the following values for you based on the properties of your project:

* if your project is self-contained, we use the `mcr.microsoft.com/dotnet/runtime-deps` image as the base image
* if your project is an ASP.NET Core project, we use the `mcr.microsoft.com/dotnet/aspnet` image as the base image
* otherwise we use the `mcr.microsoft.com/dotnet/runtime` image as the base image

We infer the tag of the image to be the numeric component of your chosen `TargetFramework` - so a `.net6.0` project will use the `6.0` tag of the inferred base image, a `.net7.0-linux` project will use the `7.0` tag, and so on.

If you set a value here, you should set the fully-qualified name of the image to use as the base, including any tag you prefer:

```xml
<PropertyGroup>
    <ContainerBaseImage>mcr.microsoft.com/dotnet/runtime:6.0</ContainerBaseImage>
<PropertyGroup>
```

### ContainerFamily

Starting in .NET 8, `ContainerFamily` can be used to choose a different family of Microsoft-provided container images as the base image for your application. When set, this value will be appended to the end of the selected TFM-specific tag, changing the tag provided. For example, to use the Alpine Linux variants of the .NET base images, you can set `ContainerFamily` to `alpine`:

```xml
<PropertyGroup>
    <ContainerFamily>alpine</ContainerFamily>
</PropertyGroup>
```

which results in a final tag of `8.0-alpine` for a .NET 8-targeting application.

This field is very free-form, and often can be used to select different operating system distributions, default package configurations, or any other 'flavor' of changes to a base image. Consult your base image's documentation for more details.

### ContainerRuntimeIdentifier

This property controls the OS and platform used by your container if your [`ContainerBaseImage`](#containerbaseimage) is a 'Manifest List'. Manifest Lists are images that support more than one architecture behind a single, common, name. For example, the `mcr.microsoft.com/dotnet/runtime` image is a manifest list that supports the `linux-x64`, `linux-arm`, `linux-arm64` images.

When a Manifest List is your base image, we need to choose the most relevant image to use as the base. We do this by choosing the image that best matches the `RuntimeIdentifier` of your project. If you set a value here, we will use that value to choose the best image to use as the base. Valid values for this property will vary based on the image you choose, but will always be in the form of a .NET SDK Runtime Identifier.

By default, if your project has a RuntimeIdentifier set, that value will be used. Starting with the .NET SDK 7.0.400, if no ContainerRuntimeIdentifier or RuntimeIdentifier is set, the `linux-x64` ContainerRuntimeIdentifier will be used. A RuntimeIdentifier is usually set via the `-r` parameter to the `dotnet publish` command, or by setting the `RuntimeIdentifier` property in a PublishProfile used from Visual Studio.

```xml
<PropertyGroup>
    <ContainerRuntimeIdentifier>linux-x64</ContainerRuntimeIdentifier>
</PropertyGroup>
```

> **Note**
> If you'd like to publish to a musl-based OS like alpine (as opposed to a libc-based OS), you will need to specify the base image _including architecture_, instead of relying on
> any of the inference described above. For example, a `net7.0`-targeting application that wanted to run on alpine with the x64 architecture would use the `7.0-alpine-amd64` tag of the `mcr.microsoft.com/dotnet/runtime` image (or another base image as appropriate for your project type):
>
> ```xml
> <PropertyGroup>
>     <ContainerBaseImage>mcr.microsoft.com/dotnet/runtime:7.0-alpine-amd64</ContainerBaseImage>
> </PropertyGroup>
> ```
>
> Starting in .NET 8, however, you can use `ContainerFamily` to make this easier:
>
> ```xml
> <PropertyGroup>
>     <ContainerFamily>alpine</ContainerFamily>
> </PropertyGroup>
> ```

> **Note**
> Starting in .NET 8, the Microsoft container images will not include the Windows variants of the images in the manifest list. If you need to target Windows, you will need to use a specific image tag as your `<ContainerBaseImage>`, for example `mcr.microsoft.com/dotnet/aspnet:8.0-preview-windowsservercore-ltsc2022` or `mcr.microsoft.com/dotnet/aspnet:8.0-preview-nanoserver-ltsc2022`.

## Modifying the generated container

These properties and items control metadata about the container that's generated for your application.

### ContainerRegistry

This property controls the destination registry - the place that the newly-created image will be pushed to.

Be default, we push to the local Docker daemon (annotated by `docker://`), but you can also specify a remote registry. Interacting with that registry may require authentication, see [Authenticating to container registries](./RegistryAuthentication.md) for more details.

```xml
<PropertyGroup>
    <ContainerRegistry>registry.mycorp.com:1234</ContainerRegistry>
</PropertyGroup>
```

### ContainerRepository

This property controls the name of the generated image itself, e.g `dotnet/runtime` or `my-awesome-app`.

By default, the value used will be the `AssemblyName` of the project. In previous versions, this property was called `ContainerImageName` - that name is still supported for backwards compatibility but will issue a warning.

```xml
<PropertyGroup>
    <ContainerRepository>my-super-awesome-app</ContainerRepository>
</PropertyGroup>
```

> **Note**
> Image names consist of one or more slash-delimited segments, each of which can only contain lowercase alphanumeric characters, periods, underscores, and dashes, and must start with a letter or number - any other characters will result in an error being thrown.

### ContainerImageTag(s)

This property controls the tag that is generated for the image. Tags are often used to refer to different versions of an application, but they can also refer to different operating system distributions, or even just different baked-in configuration. This property also can be used to push multiple tags - simply use a semicolon-delimited set of tags in the `ContainerImageTags` property, similar to setting multiple `TargetFrameworks`.

By default in .NET 8, the value used will be `latest` - keeping in line with experiences offered by other container tooling. In previous versions, the default value was the `Version` of the project. To continue using the `Version`,

_Set an explicit tag:_

```xml
<PropertyGroup>
    <ContainerImageTag>1.2.3-alpha2</ContainerImageTag>
</PropertyGroup>
```

_Set the tag based on the apps `Version`:_

```xml
<PropertyGroup>
    <ContainerImageTag>$(Version)</ContainerImageTag>
</PropertyGroup>
```

_Set multiple tags (tags are `;` delimited):_

```xml
<PropertyGroup>
    <ContainerImageTags>1.2.3-alpha2;latest</ContainerImageTags>
</PropertyGroup>
```

> **Note**
> Tags can only contain up to 127 alphanumeric characters, periods, underscores, and dashes. They must start with an alphanumeric character or an underscore. Any other form will result in an error being thrown.

### ContainerLabel

This item adds a metadata label to the container. Labels have no impact on the container at runtime, but are often used to store version and authoring metadata for use by security scanners and other infrastructure tools.

ContainerLabel items have two properties:

* Include
  * The key of the label
* Value
  * The value of the label - this may be empty

See [default container labels](#default-container-labels) for a list of labels that are created by default.

```xml
<ItemGroup>
    <ContainerLabel Include="org.contoso.businessunit" Value="contoso-university" />
</ItemGroup>
```

## Controlling container execution

These properties and items control how the generated container is executed.

### ContainerWorkingDirectory

This property controls the working directory of the container - the directory that commands are executed within if not other command is run.

By default, we use the `/app` directory as the working directory.

```xml
<PropertyGroup>
    <ContainerWorkingDirectory>/bin</ContainerWorkingDirectory>
</PropertyGroup>
```

### ContainerPort

This item adds TCP or UDP ports to the list of known ports for the container. This enables container runtimes like Docker to map these ports to the host machine automatically. This is often used as documentation for the container, but can also be used to enable automatic port mapping.

In .NET 8, we will infer `ContainerPort` data for your application based on several well-known ASP.NET Environment variables:

* ASPNETCORE_URLS
* ASPNETCORE_HTTP_PORTS
* ASPNETCORE_HTTPS_PORTS

The values of the environment variables, if present, will be parsed and converted to TCP port mappings. These environment variables will be read from your base image, if present, or from the environment variables defined in your project through `ContainerEnvironmentVariable` items.

ContainerPort items have two properties:

* Include
  * The port number to expose
* Type
  * One of `tcp` or `udp` - the default is `tcp`

```xml
<ItemGroup>
    <ContainerPort Include="80" Type="tcp" />
</ItemGroup>
```

> **Note**
> This item typically does not influence running the container directly - most runtimes will still need to explicitly assign port mappings. Some tooling, like the Docker Tools for Visual Studio Code, will read this data and automatically open ports on your behalf.


### ContainerEnvironmentVariable

This item adds a new environment variable to the container. Environment variables will be accessible to the application running in the container immediately, and are often used to change the runtime behavior of the running application.

ContainerEnvironmentVariable items have two properties:

* Include
  * The name of the environment variable
* Value
  * The value of the environment variable

```xml
<ItemGroup>
  <ContainerEnvironmentVariable Include="LOGGER_VERBOSITY" Value="Trace" />
</ItemGroup>
```

## Controlling the container's commands

By default, the container tools will launch your application using either the generated AppHost binary for your application (if your application uses an AppHost), or the `dotnet` command plus your application's DLL. However, you can control how your application is executed by using some combination of `ContainerAppCommand`, `ContainerAppCommandArgs`, `ContainerDefaultArgs`, and `ContainerAppCommandInstruction`.

We have these different configuration points because different base images use different combinations of the container Entrypoint and Command properties, and we want to be able to support all of them. Our defaults should be useable for most apps, but if you want to customize your app launch behavior you should:

* identify the binary to run and set it as `ContainerAppCommand`
* identify which arguments are _required_ for your application to run and set them as `ContainerAppCommandArgs`
* identify which arguments (if any) are _optional_ and could be overridden by a user and set them as `ContainerDefaultArgs`
* set `ContainerAppCommandInstruction` to `DefaultArgs`

More detail on each of these configuration points is detailed below:

### ContainerEntrypoint

> **Note**
> In .NET 8 this item is deprecated. See [ContainerAppCommand](#containerappcommand) for more details.

This item can be used to customize the entrypoint of the container - the binary that is run by default when the container is started.

By default, for builds that create an executable binary that binary is set as the ContainerEntrypoint. For builds that do not create an executable binary `dotnet path/to/application.dll` is used as the ContainerEntrypoint.

ContainerEntrypoint items have one property:

* Include
  * The command, option, or argument to use in the entrypoint command

```xml
<ItemGroup Label="Entrypoint Assignment">
  <!-- This is how you would start the dotnet ef tool in your container -->
  <ContainerEntrypoint Include="dotnet" />
  <ContainerEntrypoint Include="ef" />

  <!-- This shorthand syntax means the same thing - note the semicolon separating the tokens. -->
  <ContainerEntrypoint Include="dotnet;ef" />
</ItemGroup>
```

### ContainerEntrypointArgs

> **Note**
> In .NET 8 this item is deprecated. See [ContainerAppCommandArgs](#containerappcommandargs) for more details.

This item controls the default arguments provided to the `ContainerEntrypoint`. This should be used when the ContainerEntrypoint is a program that the user might want to use on its own.

By default, no ContainerEntrypointArgs are created on your behalf.

ContainerEntrypointArg items have one property:

* Include
  * The option or argument to apply to the ContainerEntrypoint command

```xml
<ItemGroup>
  <!-- Assuming the ContainerEntrypoint defined above, this would be the way to update the database by default, but let the user run a different EF command. -->
  <ContainerEntrypointArgs Include="database" />
  <ContainerEntrypointArgs Include="update" />

  <!-- This is the shorthand syntax for the same idea -->
  <ContainerEntrypointArgs Include="database;update" />
</ItemGroup>
```

### ContainerAppCommand

This item is the logical entry point of your application. For most apps, this will be the 'apphost' - the generated executable binary for your application. If your application doesn't generate an apphost, then this command will typically be `dotnet <your project dll>`. These values will be applied after any Entrypoint in your base container, or directly if no Entrypoint is defined.

ContainerAppCommand items have one property:

* Include
  * The command, option, or argument to use in the entrypoint command

```xml
<ItemGroup Label="ContainerAppCommand Assignment">
  <!-- This is how you would start the dotnet ef tool in your container -->
  <ContainerAppCommand Include="dotnet" />
  <ContainerAppCommand Include="ef" />

  <!-- This shorthand syntax means the same thing - note the semicolon separating the tokens. -->
  <ContainerAppCommand Include="dotnet;ef" />
</ItemGroup>
```

### ContainerAppCommandArgs

This item represents any logically-required arguments for your application that should be applied to the `ContainerAppCommand`. By default, none are generated for an application. If present, these will always be applied to your container when it is run.

ContainerAppCommandArgs items have one property:

* Include
  * The option or argument to apply to the ContainerAppCommand command

```xml
<ItemGroup>
  <!-- Assuming the ContainerAppCommand defined above, this would be the way to force the database to update. -->
  <ContainerAppCommandArgs Include="database" />
  <ContainerAppCommandArgs Include="update" />

  <!-- This is the shorthand syntax for the same idea -->
  <ContainerAppCommandArgs Include="database;update" />
</ItemGroup>
```

### ContainerDefaultArgs

This item represents any user-overrideable arguments for your application.  This is a good way to provide any defaults that your app might need to run in a way that makes it easy to start your application, but still easy to customize.

ContainerDefaultArgs items have one property:

* Include
  * The option or argument to apply to the ContainerAppCommand command

```xml
<ItemGroup>
  <!-- Assuming the ContainerAppCommand defined above, this would be the way to force the database to update. -->
  <ContainerDefaultArgs Include="database" />
  <ContainerDefaultArgs Include="update" />

  <!-- This is the shorthand syntax for the same idea -->
  <ContainerDefaultArgs Include="database;update" />
</ItemGroup>
```


### ContainerAppCommandInstruction

> **Note**
> This is advanced stuff - most applications shouldn't need to customize their entrypoint to this degree. We'd love to hear more about your use case at https://github.com/dotnet/sdk-container-builds/discussions.

This property helps control the way the `ContainerEntrypoint`, `ContainerEntrypointArgs`, `ContainerAppCommand`, `ContainerAppCommandArgs`, and `ContainerDefaultArgs` are combined to form the final command that is run in the container. This depends greatly on if an Entrypoint is present in the base image. This property can take one of three values: "DefaultArgs", "Entrypoint", or "None".

* Entrypoint
  * In this mode, the entrypoint is purely defined by `ContainerAppCommand`, `ContainerAppCommandArgs`, and `ContainerDefaultArgs`.
* None
  * In this mode, the entrypoint is purely defined by `ContainerEntrypoint`, `ContainerEntrypointArgs`, and `ContainerDefaultArgs`.
* DefaultArgs
  * This is the most complex mode - if none of the `ContainerEntrypoint[Args]` items are present, we defer to `ContainerAppCommand[Args]` and `ContainerDefaultArgs` to create the entrypoint and command - skipping the base image entrypoint for base images that have it hard-coded to `dotnet` or `/usr/bin/dotnet` so that you have complete control.
  * If both `ContainerEntrypoint` and `ContainerAppCommand` are present, then `ContainerEntrypoint` becomes the entrypoint, and `ContainerAppCommand` becomes the command.


### ContainerUser

This item controls the default user that the container will run as. This is often used to run the container as a non-root user, which is a best practice for security. There are a few constraints to know about this field:

* It can take a variety of forms - user name, linux user ids, group name, linux group id, `username:groupname`, id variants of the above
* There is no verification that the user or group specified exists on the image
* Changing the user can alter the behavior of the application, especially in regards to things like File System permissions

The default value of this field varies by project TFM and target operating system:

* if you are targeting .NET 8 or higher and using the Microsoft runtime images, then
  * on Linux the rootless user `app` will be used (though it will be referenced by its user id)
  * on Windows the rootless user `ContainerUser` will be used
* otherwise no default `ContainerUser` will be used

```xml
<PropertyGroup>
    <ContainerUser>my-existing-app-user</ContainerUser>
</PropertyGroup>
```

> **Note**
> In .NET 8 the APP_UID environment variable will be used to set user information in your container. This value can come from environment variables defined in your base image (like that Microsoft .NET images do), or you can set it yourself via the `ContainerEnvironmentVariable` syntax described in this document.

## Default container labels

Labels are often used to provide consistent metadata on container images. This package provides some default labels to encourage better maintainability of the generated images, drawn from the set defined as part of the [OCI Image specification](https://github.com/opencontainers/image-spec/blob/main/annotations.md). Where possible, we use the values of common [NuGet Project Properties](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets#pack-target) as defaults for these annotations, though we also provide more specific properties for each of these labels.

| Annotation | Default Value | Dedicated Property Name | Fallback Property Name | Enabled Property Name | Notes |
| - | - | - | - | - | - |
| `org.opencontainers.image.created` and `org.opencontainers.artifact.created` | the [RFC 3339](https://tools.ietf.org/html/rfc3339#section-5.6) format of the current UTC DateTime |  |  | `ContainerGenerateLabelsImageCreated` |  |
|`org.opencontainers.artifact.description` and `org.opencontainers.image.description` | | `ContainerDescription` | `Description` | `ContainerGenerateLabelsImageDescription` | |
| `org.opencontainers.image.authors` | | `ContainerAuthors`| `Authors` | `ContainerGenerateLabelsImageAuthors` | |
| `org.opencontainers.image.url` | | `ContainerInformationUrl` | `PackageProjectUrl` | `ContainerGenerateLabelsImageUrl` | |
| `org.opencontainers.image.documentation` | | `ContainerDocumentationUrl` | `PackageProjectUrl` | `ContainerGenerateLabelsImageDocumentation` | |
| `org.opencontainers.image.version` | | `ContainerVersion` | `PackageVersion` | `ContainerGenerateLabelsImageVersion` | |
| `org.opencontainers.image.vendor` | | `ContainerVendor` | | `ContainerGenerateLabelsImageVendor` | |
| `org.opencontainers.image.licenses` | | `ContainerLicenseExpression` | `PackageLicenseExpression` | `ContainerGenerateLabelsImageLicenses` | |
| `org.opencontainers.image.title` | | `ContainerTitle` | `Title` | `ContainerGenerateLabelsImageTitle` | |
| `org.opencontainers.image.base.name` | | `ContainerBaseImage` | | `ContainerGenerateLabelsImageBaseName` | |
| `org.opencontainers.image.source` | | `PrivateRepositoryUrl` | | `ContainerGenerateLabelsImageSource` | Only written if `PublishRepositoryUrl` is `true`. Also relies on Sourcelink infrastructure being part of the build. |
| `org.opencontainers.image.revision` | | `SourceRevisionId` | | `ContainerGenerateLabelsImageRevision` | Only written if `PublishRepositoryUrl` is `true`. Also relies on Sourcelink infrastructure being part of the build. |

> **Note**
> You can disable all label generation by setting `ContainerGenerateLabels` to `false` in your project file.
