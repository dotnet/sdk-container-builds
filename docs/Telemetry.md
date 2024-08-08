# Telemetry reporting in the .NET SDK Container tools

Starting in .NET SDK 8.0.400, the Container tooling collects and sends usage telemetry about how the tools are used.
This is in addition to the [telemetry sent by the CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/telemetry), but uses the same mechanisms and, importantly, adheres to the same [opt-out](https://learn.microsoft.com/en-us/dotnet/core/tools/telemetry#how-to-opt-out) controle.

The telemetry we gather is intended to be general in nature and not leak any personal information - we intend to use this telemetry to help us measure
* usage of the SDK Containerization feature overall
* success and failure rates, along with general information about what kinds of failures happen most frequently
* usage of specific features of the tech, like publishing to various registry kinds, or how the publish was invoked

## Inference telemetry

We log the following information about how the base image inference process occcurred:

| Data Point | Explanation | Sample value |
| - | - | - |
| InferencePerformed | If users are manually specifying base images vs making use of inference. | true |
| TargetFramework | The TargetFramework chosen when doing base image inference. | net8.0 |
| BaseImage | The value of the base image chosen, but only if that base image is one of the Microsoft-produced images. If a user specifies any image other than the Microsoft-produced images on mcr.microsoft.com, this value is null. | mcr.microsoft.com/dotnet/aspnet | 
| BaseImageTag | The value of the tag chosen, but only if that tag is for one of the Microsoft-produced images. If a user specifies any image other than the Microsoft-produced images on mcr.microsoft.com, this value is null. | 8.0 |
| ContainerFamily | The value of the `ContainerFamily` property if a user used the ContainerFamily feature to pick a 'flavor' of one of our base images. This is only set if the user picked or inferred one of the Microsoft-produced .NET images from mcr.microsoft.com | jammy-chiseled |
| ProjectType | What kind of project was containerized | AspNetCore or Console |
| PublishMode | How the application was packaged | Aot, Trimmed, SelfContained, or FrameworkDependent |
| IsInvariant | If the image chosen requires invariant globalization or the user opted into it manually | true |
| TargetRuntime | The RID that this application was published for | linux-x64 |

## Image creation telemetry

We log the following information about how the container creation and publishing process occurred:

| Data Point | Explanation | Sample value |
| - | - | - |
| RemotePullType | If the base image came from a remote registry, what kind of registry was it? |  Azure, AWS, Google, GitHub, DockerHub, MRC, or Other |
| LocalPullType | If the base image came from a local source, like a container daemon or a tarball. Note - the container tools currently do not support using local sources for base images, this information is added looking forward to that future feature. | Docker, Podman, Tarball | 
| RemotePushType | If the image was pushed to a remote registry, what kind of registry was it? |   Azure, AWS, Google, GitHub, DockerHub, MRC, or Other |
| LocalPushType | If the image was pushed to a local destination, what was it? | Docker, Podman, Tarball |

In addition, if various kinds of errors occur during the process we collect data about what kind of error it was:

| Data Point | Explanation | Sample value |
| - | - | - |
| Error | The kind of error that occurred | unknown_repository, credential_failure, rid_mismatch, local_load - there are more kinds of errors but we expect the kinds to grow over time |
| Direction | If the error is a credential_failure, was it to the push or pull registry? | push |
| Target RID | If the error was a rid_mismatch, what RID was requested | linux-x64 |
| Available RIDs | If the error was a rid_mismatch, what RIDs did the base image support? | linux-x64,linux-arm64 |
