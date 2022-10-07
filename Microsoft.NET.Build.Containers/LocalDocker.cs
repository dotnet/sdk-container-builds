using System.Diagnostics;
using System.Formats.Tar;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.NET.Build.Containers;

public class LocalDocker
{
    public static async Task Load(Image x, string name, string tag, string baseName)
    {
        // call `docker load` and get it ready to receive input
        ProcessStartInfo loadInfo = new("docker", $"load");
        loadInfo.RedirectStandardInput = true;
        loadInfo.RedirectStandardOutput = true;
        loadInfo.RedirectStandardError = true;

        using Process? loadProcess = Process.Start(loadInfo);

        if (loadProcess is null)
        {
            throw new NotImplementedException("Failed creating docker process");
        }

        // Create new stream tarball

        await WriteImageToStream(x, name, tag, loadProcess.StandardInput.BaseStream);

        loadProcess.StandardInput.Close();

        await loadProcess.WaitForExitAsync();

        if (loadProcess.ExitCode != 0)
        {
            throw new DockerLoadException($"Failed to load image to local Docker daemon. stdout: {await loadProcess.StandardError.ReadToEndAsync()}");
        }
    }

    public static async Task<Image> Pull(string name, string reference) {
        // call 'docker save' to save the tarball out to a stream
        // extract it to the relevant locations in the content store
        // locate the manifest and return it
        ProcessStartInfo saveInfo = new("docker", "save ${name}:{reference}"){
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true 
        };

        using Process? saveProcess = Process.Start(saveInfo);
        if(saveProcess is null) {
            throw new NotImplementedException("Failed creating docker save process");
        }

        using var reader = new TarReader(saveProcess.StandardOutput.BaseStream, leaveOpen: true);
        JsonNode? manifest = null;
        JsonNode? config = null;
        HashSet<string> knownDirectories = new();

        void ReadManifest(TarEntry entry) {
            using var dataStream = entry.DataStream;
            manifest = JsonNode.Parse(dataStream);
        }

        void ReadConfig(TarEntry entry) {
            using var dataStream = entry.DataStream;
            config = JsonNode.Parse(dataStream);
        }

        void AddTrackedDirectory(TarEntry entry) {
            knownDirectories.Add(entry.Name);
        }

        async Task ExtractTarToContentStore(TarEntry entry) {
            var parentDir = Path.GetDirectoryName(entry.Name);
            // the descriptor hash is the parent dir
            var descriptorHash = Path.GetFileName(parentDir)!;
            var tarSize = entry.DataStream.Length!;
            var contentStorePath = ContentStore.PathForDescriptor(new Descriptor("application/vnd.docker.image.rootfs.diff.tar.gzip", descriptorHash, tarSize));
            using var dataStream = entry.DataStream;
            if(File.Exists(contentStorePath)) return;

            using var layerFile = File.Create(contentStorePath); 
            await dataStream.CopyToAsync(layerFile);
        }

        while (true) {
            var entry = await reader.GetNextEntryAsync();
            if(entry is null) break;
            if (entry is { EntryType: TarEntryType.RegularFile }) {
                if (Path.GetFileName(entry.Name) == "manifest.json") {
                    ReadManifest(entry); 
                } else if (Path.GetExtension(entry.Name) == ".json") {
                    ReadConfig(entry);
                } else if (Path.GetFileName(entry.Name) == "layer.tar"
                            && Path.GetDirectoryName(entry.Name) is string parentDir
                            && knownDirectories.Contains(parentDir)) {
                    await ExtractTarToContentStore(entry);
                }
            } else if (entry is { EntryType: TarEntryType.Directory }) { 
                AddTrackedDirectory(entry);
            }
        }

        if (manifest is not null && config is not null) {
            return new Image(manifest, config, name, null);
        } else {
            throw new Exception("Unable to load image from Docker daemon");
        }
    }

    public static async Task WriteImageToStream(Image x, string name, string tag, Stream imageStream)
    {
        TarWriter writer = new(imageStream, TarEntryFormat.Gnu, leaveOpen: true);


        // Feed each layer tarball into the stream
        JsonArray layerTarballPaths = new JsonArray();

        foreach (var d in x.LayerDescriptors)
        {
            if (!x.originatingRegistry.HasValue)
            {
                throw new NotImplementedException("Need a good error for 'couldn't download a thing because no link to registry'");
            }

            string localPath = await x.originatingRegistry.Value.DownloadBlob(x.OriginatingName, d);

            // Stuff that (uncompressed) tarball into the image tar stream
            // TODO uncompress!!
            string layerTarballPath = $"{d.Digest.Substring("sha256:".Length)}/layer.tar";
            await writer.WriteEntryAsync(localPath, layerTarballPath);
            layerTarballPaths.Add(layerTarballPath);
        }

        // add config
        string configTarballPath = $"{Image.GetSha(x.config)}.json";

        using (MemoryStream configStream = new MemoryStream(Encoding.UTF8.GetBytes(x.config.ToJsonString())))
        {
            GnuTarEntry configEntry = new(TarEntryType.RegularFile, configTarballPath)
            {
                DataStream = configStream
            };

            await writer.WriteEntryAsync(configEntry);
        }

        // Add manifest
        JsonArray tagsNode = new()
        {
            name + ":" + tag
        };

        JsonNode manifestNode = new JsonArray(new JsonObject
        {
            { "Config", configTarballPath },
            { "RepoTags", tagsNode },
            { "Layers", layerTarballPaths }
        });

        using (MemoryStream manifestStream = new MemoryStream(Encoding.UTF8.GetBytes(manifestNode.ToJsonString())))
        {
            GnuTarEntry manifestEntry = new(TarEntryType.RegularFile, "manifest.json")
            {
                DataStream = manifestStream
            };

            await writer.WriteEntryAsync(manifestEntry);
        }
    }
}
