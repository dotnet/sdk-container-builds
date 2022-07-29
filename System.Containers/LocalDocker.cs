using System.Diagnostics;
using System.Formats.Tar;
using System.Text;

namespace System.Containers;

internal class LocalDocker
{
    public async Task Load(Image x, string name, string baseName)
    {
        // call `docker load` and get it ready to recieve input
        ProcessStartInfo loadInfo = new("docker", $"load");
        loadInfo.RedirectStandardInput = true;
        loadInfo.RedirectStandardOutput = true;

        using Process? loadProcess = Process.Start(loadInfo);

        if (loadProcess is null)
        {
            throw new NotImplementedException("Failed creating docker process");
        }

        // Create new stream tarball

        await WriteImageToStream(x, name, baseName, loadProcess.StandardInput.BaseStream);

        await loadProcess.WaitForExitAsync();



        // give it a tag?
    }

    private static async Task WriteImageToStream(Image x, string name, string baseName, Stream imageStream)
    {
        // Populate local cache with all layer tarballs

        TarWriter writer = new(imageStream, TarEntryFormat.Gnu, leaveOpen: true);

        // Feed each layer tarball into the stream

        // add config
        using (MemoryStream configStream = new MemoryStream(Encoding.UTF8.GetBytes(x.config.ToJsonString())))
        {
            GnuTarEntry configEntry = new(TarEntryType.RegularFile, $"{x.GetSha(x.config)}.json")
            {
                DataStream = configStream
            };

            writer.WriteEntry(configEntry); // TODO: asyncify these when API available (Preview 7)
        }

        // Add manifest

    }
}
