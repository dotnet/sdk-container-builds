// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.Resources;
using System.Formats.Tar;
using System.Text.Json.Nodes;
using System.Text;

namespace Microsoft.NET.Build.Containers.Outputs
{
    internal sealed class FileOutput
    {
        private readonly Action<string> logger;

        public FileOutput(Action<string> logger)
        {
            this.logger = logger;
        }

        // This test use local registry to get some information like the sha256
        public async Task Export(string outputFilePath, BuiltImage image, ImageReference sourceReference, ImageReference destinationReference)
        {
            try
            {
                using var streamWriter = new StreamWriter(outputFilePath);
                await WriteImageToStream(image, sourceReference, destinationReference, streamWriter.BaseStream).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger(ex.Message);
            }
        }

        // This function is the same on LocalDocker.cs file. We could refactor to use in multiple places
        private static async Task WriteImageToStream(BuiltImage image, ImageReference sourceReference, ImageReference destinationReference, Stream imageStream)
        {
            using TarWriter writer = new(imageStream, TarEntryFormat.Pax, leaveOpen: true);

            // Feed each layer tarball into the stream
            JsonArray layerTarballPaths = new JsonArray();

            foreach (var d in image.LayerDescriptors)
            {
                if (sourceReference.Registry is { } registry)
                {
                    string localPath = await registry.DownloadBlob(sourceReference.Repository, d).ConfigureAwait(false); ;

                    // Stuff that (uncompressed) tarball into the image tar stream
                    // TODO uncompress!!
                    string layerTarballPath = $"{d.Digest.Substring("sha256:".Length)}/layer.tar";
                    await writer.WriteEntryAsync(localPath, layerTarballPath).ConfigureAwait(false);
                    layerTarballPaths.Add(layerTarballPath);
                }
                else
                {
                    throw new NotImplementedException(Resource.GetString(nameof(Strings.MissingLinkToRegistry)));
                }
            }

            // add config
            string configTarballPath = $"{image.ImageSha}.json";

            using (MemoryStream configStream = new MemoryStream(Encoding.UTF8.GetBytes(image.Config)))
            {
                PaxTarEntry configEntry = new(TarEntryType.RegularFile, configTarballPath)
                {
                    DataStream = configStream
                };

                await writer.WriteEntryAsync(configEntry).ConfigureAwait(false);
            }

            // Add manifest
            JsonArray tagsNode = new()
            {
                destinationReference.RepositoryAndTag
            };

            JsonNode manifestNode = new JsonArray(new JsonObject
            {
                { "Config", configTarballPath },
                { "RepoTags", tagsNode },
                { "Layers", layerTarballPaths }
            });

            using (MemoryStream manifestStream = new MemoryStream(Encoding.UTF8.GetBytes(manifestNode.ToJsonString())))
            {
                PaxTarEntry manifestEntry = new(TarEntryType.RegularFile, "manifest.json")
                {
                    DataStream = manifestStream
                };
                await writer.WriteEntryAsync(manifestEntry).ConfigureAwait(false);
            }
        }
    }
}
