# Authenticating to container registries

Interacting with private container registries requires authenticating with those registries.

Docker has established a pattern with this via the [`docker login`](https://docs.docker.com/engine/reference/commandline/login/) command, which is a way of interacting with a Docker config file that contains rules for authenticating with specific registries. This file, and the authentication types it encodes, are supported by Microsoft.Net.Build.Containers for registry authentication. This should ensure that this package works seamlessly with any registry you can `docker pull` from and `docker push`. This file is normally stored at `~/.docker/config.json`, but it can be specified additionally through the `DOCKER_CONFIG` variable, which points to a directory containing a `config.json` file.

## Kinds of authentication

The config.json file contains three kinds of authentication:

* explicit username/password
* credential helpers
* system keychain

### Explicit username/password

The `auths` section of the config.json file is a key/value map between registry names and Base64-encoded username:password strings.  In a 'default' Docker scenario, running `docker login <registry> -u <username> -p <password>` will create new items in this map. These kinds of credentials are popular in Continuous Integration systems, where logging in is done by tokens at the start of a run, but are less popular for end-user development machines, where having bare credentials in a file is a security risk.

### Credential helpers

The `credHelpers` section of the config.json file is a key/value map between registry names and the names of specific programs that can be used to create and retrieve credentials for that registry. This is often used when particular registries have complex authentication requirements. In order for this kind of authentication to work, you must have an application named `docker-credential-{name}` on your system's PATH.  These kinds of credentials tend to be very secure, but can be hard to setup on development or CI machines.

### System Keychains

The `credsStore` section is a single string property whose value is the name of a docker credential helper program that knows how to interface with the system's password manager. For Windows this might be `wincred` for example. These are very popular with Docker installers for MacOS and Windows.


## Authentication via environment variables

In some scenarios the standard Docker authentication mechanism described above just doesn't cut it. This tooling has an additional mechanism for providing credentials to registries: environment variables. If environment variables are used, the credential provide mechanism will not be used at all. The following environment variables are supported:

* SDK_CONTAINER_REGISTRY_UNAME
  * This should be the username for the registry. If the password for the registry is a token, then the username should be `"<token>"`.
* SDK_CONTAINER_REGISTRY_PWORD
  * This should be the password, token, etc for the registry.

This mechanism is potentially vulnerable to credential leakage, so it should only be used in scenarios where the other mechanism is not available. For example, if you are using the SDK Container tooling inside a Docker container itself. In addition, this mechanism isn't namespaced - it will attempt to use the same credentials for both the 'source' registry (where your base image is located) as well as the 'destination' registry (where you are pushing your final image).

## Known-supported registries

All of the above mechanisms are supported by this package. When we push or pull from a registry we will incorporate these credential helpers and invoke them to get any necessary credentials the registry asks for.

The following registries have been explicitly tested:

* Azure Container Registry
* GitLab Container Registry
* Google Cloud Artifact Registry
* Quay.io
* AWS Elastic Container Registry
* GitHub Package Registry
* Docker Hub*

## Known-unsupported registries

None! We're compatible with most registries.

## Notes for specific registries

### Docker Hub

When using Docker Hub as a base image registry (via ContainerBaseImage) or as the destination registry for pushing your images (via ContainerRegistry), you must use the one of the URLs that point to the _registry_ portion of Docker Hub. This means one of the following domains must be used:

* `registry.hub.docker.com`
* `registry-1.docker.io`

The `docker.io` domain doesn't support the Registry API, so attempting to use it will result in errors.

In addition, you should be sure to login via `docker login registry.hub.docker.com` or `docker login registry-1.docker.io` and not `docker login docker.io`, to ensure that the correct credentials are used by the tooling.

#### ContainerImageName

When pushing to Docker Hub, images _must_ include the user's login as a prefix - for example `chusk3/sdk-container-demo` instead of just `sdk-container-demo`.
