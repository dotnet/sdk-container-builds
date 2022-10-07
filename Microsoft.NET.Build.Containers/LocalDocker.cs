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

    public static Image Pull(string name, string reference) {
        // call 'docker save' to save the tarball out to a stream
        // extract it to the relevant locations in the content store
        // locate the manifest and return it
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
        File.Create(tempFile).Dispose();
        Console.WriteLine($"running 'docker save {name}:{reference}'");
        ProcessStartInfo saveInfo = new("docker", $"save --output \"{tempFile}\" {name}:{reference}"){
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        Process? saveProcess = Process.Start(saveInfo);
        if(saveProcess is null) {
            throw new NotImplementedException("Failed creating docker save process");
        }
        saveProcess.WaitForExit();
        if (saveProcess.ExitCode != 0) {
            throw new Exception($"error saving tarball:\n{saveProcess.StandardOutput.ReadToEnd()}\n{saveProcess.StandardError.ReadToEnd()}");
        }

        using var reader = new TarReader(File.OpenRead(tempFile), leaveOpen: false);
        JsonNode? manifest = null;
        JsonNode? config = null;
        HashSet<string> knownDirectories = new();

        void ReadManifest(TarEntry entry) {
            Console.WriteLine($"Reading entry {entry.Name} as manifest");
            var dataStream = entry.DataStream;
            manifest = JsonNode.Parse(dataStream);
        }

        void ReadConfig(TarEntry entry) {
            Console.WriteLine($"Reading entry {entry.Name} as config");
            var dataStream = entry.DataStream;
            config = JsonNode.Parse(dataStream);
        }

        void AddTrackedDirectory(TarEntry entry) {
            Console.WriteLine($"Tracking directory {entry.Name.TrimEnd('/')}");
            knownDirectories.Add(entry.Name.TrimEnd('/'));
        }

        void ExtractTarToContentStore(TarEntry entry) {
            
            var parentDir = Path.GetDirectoryName(entry.Name);
            // the descriptor hash is the parent dir
            var descriptorHash = Path.GetFileName(parentDir)!;
            Console.WriteLine($"Extracting entry {descriptorHash}");
            var contentStorePath = ContentStore.PathForDescriptor(new Descriptor("application/vnd.docker.image.rootfs.diff.tar", descriptorHash, 0));
            Console.WriteLine($"Extracting entry {descriptorHash} to {contentStorePath}");
            var dataStream = entry.DataStream;
            if(File.Exists(contentStorePath)) {
                Console.WriteLine($"Entry {descriptorHash} already existed");
                return;
            }
            entry.ExtractToFile(contentStorePath, overwrite: true);
            Console.WriteLine($"Copied entry {descriptorHash}");
        }

        while (true) {
            var entry = reader.GetNextEntry(copyData: true);
            if(entry is null) {
                Console.WriteLine("done reading");
                break;
            }
            Console.WriteLine($"Considering entry {entry.Name} of type {entry.EntryType}");
            if (entry is { EntryType: TarEntryType.RegularFile }) {
                if (Path.GetFileName(entry.Name) == "manifest.json") {
                    ReadManifest(entry); 
                } else if (Path.GetExtension(entry.Name) == ".json") {
                    ReadConfig(entry);
                } else if (Path.GetFileName(entry.Name) == "layer.tar"
                            && Path.GetDirectoryName(entry.Name) is string parentDir
                            && knownDirectories.Contains(parentDir)) {
                    ExtractTarToContentStore(entry);
                }
            } else if (entry is { EntryType: TarEntryType.Directory }) { 
                AddTrackedDirectory(entry);
            }
        }
        
        Console.WriteLine($"save process exited with code {saveProcess.ExitCode}");
        reader.Dispose();
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
