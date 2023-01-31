namespace Microsoft.NET.Build.Containers;

public static class MediaTypes {
    public const string OciImageLayerV1Tar = "application/vnd.oci.image.layer.v1.tar";
    public const string OciImageLayerV1TarGzip = $"{OciImageLayerV1Tar}+gzip";
    public const string OciImageManifestV1 = "application/vnd.oci.image.manifest.v1+json";
    public const string DockerImageRootFsDiffTar = "application/vnd.docker.image.rootfs.diff.tar";
    public const string DockerImageRootFsDiffTarGzip = $"{DockerImageRootFsDiffTar}.gzip";
    public const string DockerImageRootFsForeignDiffTar = "application/vnd.docker.image.rootfs.foreign.diff.tar";
    public const string DockerImageRootFsForeignDiffTarGzip = $"{DockerImageRootFsForeignDiffTar}.gzip";
    public const string DockerManifestV2 = "application/vnd.docker.distribution.manifest.v2+json";
    public const string DockerManifestListV2 = "application/vnd.docker.distribution.manifest.list.v2+json";
    public const string DockerContainerV1 = "application/vnd.docker.container.image.v1+json";

    public const string OctetStream = "application/octet-stream";
    public const string Json = "application/json";

}