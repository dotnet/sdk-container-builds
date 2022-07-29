using System.Diagnostics;
using System.Formats.Tar;
using System.Text;
using System.Text.Json;

namespace System.Containers;

public class LocalDocker
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

    public static async Task WriteImageToStream(Image x, string name, string baseName, Stream imageStream)
    {
        foreach (var layerJson in x.manifest["layers"].AsArray())
        {
            Descriptor d = layerJson.Deserialize<Descriptor>();

            if (!x.originatingRegistry.HasValue)
            {
                throw new NotImplementedException("Need a good error for 'couldn't download a thing because no link to registry'");
            }

            string localPath = await x.originatingRegistry.Value.LocalFileForBlob(baseName, d);
        }

        TarWriter writer = new(imageStream, TarEntryFormat.Gnu, leaveOpen: true);

        // Feed each layer tarball into the stream

        // add config
        using (MemoryStream configStream = new MemoryStream(Encoding.UTF8.GetBytes(x.config.ToJsonString())))
        {
            GnuTarEntry configEntry = new(TarEntryType.RegularFile, $"{Image.GetSha(x.config)}.json")
            {
                DataStream = configStream
            };

            writer.WriteEntry(configEntry); // TODO: asyncify these when API available (Preview 7)
        }

        // Add manifest

    }
}
