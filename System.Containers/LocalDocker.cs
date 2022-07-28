namespace System.Containers;

internal class LocalDocker
{
    public async Task Load(Image x, string name, string baseName)
    {
        // Populate local cache with all layer tarballs

        // Create new stream tarball

        // Feed each layer tarball into the stream

        // Add manifest

        // add config

        // call `docker load`

        // give it a tag?
    }
}
