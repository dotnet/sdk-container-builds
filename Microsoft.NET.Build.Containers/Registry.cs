using Microsoft.VisualBasic;

using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Microsoft.NET.Build.Containers;

public record struct Registry(Uri BaseUri)
{
    private const string DockerManifestV2 = "application/vnd.docker.distribution.manifest.v2+json";
    private const string DockerContainerV1 = "application/vnd.docker.container.image.v1+json";
    private const int MaxChunkSizeBytes = 1024 * 64;

    private string RegistryName { get; } = BaseUri.Host;

    public async Task<Image> GetImageManifest(string name, string reference)
    {
        HttpClient client = GetClient();

        var response = await client.GetAsync(new Uri(BaseUri, $"/v2/{name}/manifests/{reference}"));

        response.EnsureSuccessStatusCode();

        var s = await response.Content.ReadAsStringAsync();

        var manifest = JsonNode.Parse(s);

        if (manifest is null) throw new NotImplementedException("Got a manifest but it was null");

        if ((string?)manifest["mediaType"] != DockerManifestV2)
        {
            throw new NotImplementedException($"Do not understand the mediaType {manifest["mediaType"]}");
        }

        JsonNode? config = manifest["config"];
        Debug.Assert(config is not null);
        Debug.Assert(((string?)config["mediaType"]) == DockerContainerV1);

        string? configSha = (string?)config["digest"];
        Debug.Assert(configSha is not null);

        response = await client.GetAsync(new Uri(BaseUri, $"/v2/{name}/blobs/{configSha}"));

        JsonNode? configDoc = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Debug.Assert(configDoc is not null);
        //Debug.Assert(((string?)configDoc["mediaType"]) == DockerContainerV1);

        return new Image(manifest, configDoc, name, this);
    }

    /// <summary>
    /// Ensure a blob associated with <paramref name="name"/> from the registry is available locally.
    /// </summary>
    /// <param name="name">Name of the associated image.</param>
    /// <param name="descriptor"><see cref="Descriptor"/> that describes the blob.</param>
    /// <returns>Local path to the (decompressed) blob content.</returns>
    public async Task<string> DownloadBlob(string name, Descriptor descriptor)
    {
        string localPath = ContentStore.PathForDescriptor(descriptor);

        if (File.Exists(localPath))
        {
            // Assume file is up to date and just return it
            return localPath;
        }

        // No local copy, so download one

        HttpClient client = GetClient();

        var response = await client.GetAsync(new Uri(BaseUri, $"/v2/{name}/blobs/{descriptor.Digest}"), HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        string tempTarballPath = ContentStore.GetTempFile();
        using (FileStream fs = File.Create(tempTarballPath))
        {
            using Stream responseStream = await response.Content.ReadAsStreamAsync();

            await responseStream.CopyToAsync(fs);
        }

        File.Move(tempTarballPath, localPath, overwrite: true);

        return localPath;
    }

    public async Task Push(Layer layer, string name, Action<string> logProgressMessage)
    {
        string digest = layer.Descriptor.Digest;

        using (FileStream contents = File.OpenRead(layer.BackingFile))
        {
            await UploadBlob(name, digest, contents);
        }
    }

    private readonly async Task UploadBlob(string name, string digest, Stream contents)
    {
        HttpClient client = GetClient();

        if (await BlobAlreadyUploaded(name, digest, client))
        {
            // Already there!
            return;
        }

        Uri pushUri = new Uri(BaseUri, $"/v2/{name}/blobs/uploads/");
        HttpResponseMessage pushResponse = await client.PostAsync(pushUri, content: null);

        if (pushResponse.StatusCode != HttpStatusCode.Accepted)
        {
            string errorMessage = $"Failed to upload blob to {pushUri}; recieved {pushResponse.StatusCode} with detail {await pushResponse.Content.ReadAsStringAsync()}";
            throw new ApplicationException(errorMessage);
        }

        UriBuilder x;
        if (pushResponse.Headers.Location is {IsAbsoluteUri: true })
        {
            x = new UriBuilder(pushResponse.Headers.Location);
        }
        else
        {
            // if we don't trim the BaseUri and relative Uri of slashes, you can get invalid urls.
            // Uri constructor does this on our behalf.
            x = new UriBuilder(new Uri(BaseUri, pushResponse.Headers.Location?.OriginalString ?? ""));
        }

        Uri patchUri = x.Uri;

        x.Query += $"&digest={Uri.EscapeDataString(digest)}";

        Uri putUri = x.Uri;

        // TODO: this chunking is super tiny and probably not necessary; what does the docker client do
        //       and can we be smarter?

        byte[] chunkBackingStore = new byte[MaxChunkSizeBytes];

        int chunkCount = 0;
        int chunkStart = 0;

        while (contents.Position < contents.Length)
        {
            int bytesRead = await contents.ReadAsync(chunkBackingStore);

            ByteArrayContent content = new (chunkBackingStore, offset: 0, count: bytesRead);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Headers.ContentLength = bytesRead;
            //HttpResponseMessage putResponse = await client.PutAsync(putUri, content);

            //string resp = await putResponse.Content.ReadAsStringAsync();

            //putResponse.EnsureSuccessStatusCode();

            // Chunked upload
            //content.Headers.ContentRange = new(from:0, to: contents.Length - 1);

            // manual because ACR throws an error with the .NET type {"Range":"bytes 0-84521/*","Reason":"the Content-Range header format is invalid"}
            //content.Headers.Add("Content-Range", $"0-{contents.Length - 1}");
            Debug.Assert(content.Headers.TryAddWithoutValidation("Content-Range", $"{chunkStart}-{chunkStart + bytesRead - 1}"));

            HttpResponseMessage? patchResponse = null;
            for (int retry = 0; retry < 3; retry++)
            {
                ExceptionDispatchInfo? e = null;
                try
                {
                    patchResponse = await client.PatchAsync(patchUri, content);
                    break;
                }
                catch (Exception ex)
                {
                    e = ExceptionDispatchInfo.Capture(ex);
                }

                e.Throw();
            }

            if (patchResponse!.StatusCode != HttpStatusCode.Accepted)
            {
                string errorMessage = $"Failed to upload blob to {patchUri}; recieved {patchResponse.StatusCode} with detail {await patchResponse.Content.ReadAsStringAsync()}";
                throw new ApplicationException(errorMessage);
            }

            if (patchResponse.Headers.Location is { IsAbsoluteUri: true })
            {
                x = new UriBuilder(patchResponse.Headers.Location);
            }
            else
            {
                // if we don't trim the BaseUri and relative Uri of slashes, you can get invalid urls.
                // Uri constructor does this on our behalf.
                x = new UriBuilder(new Uri(BaseUri, patchResponse.Headers.Location?.OriginalString ?? ""));
            }

            patchUri = x.Uri;

            chunkCount += 1;
            chunkStart += bytesRead;
        }

        // PUT with digest to finalize
        x.Query += $"&digest={Uri.EscapeDataString(digest)}";

        putUri = x.Uri;

        HttpResponseMessage finalizeResponse = await client.PutAsync(putUri, content: null);

        if (finalizeResponse.StatusCode != HttpStatusCode.Created)
        {
            string errorMessage = $"Failed to finalize upload to {putUri}; recieved {finalizeResponse.StatusCode} with detail {await finalizeResponse.Content.ReadAsStringAsync()}";
            throw new ApplicationException(errorMessage);
        }
    }

    private readonly async Task<bool> BlobAlreadyUploaded(string name, string digest, HttpClient client)
    {
        HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, new Uri(BaseUri, $"/v2/{name}/blobs/{digest}")));

        if (response.StatusCode == HttpStatusCode.OK)
        {
            return true;
        }

        return false;
    }

    private static HttpClient _client = CreateClient();

    private static HttpClient GetClient()
    {
        return _client;
    }

    private static HttpClient CreateClient()
    {
        var clientHandler = new AuthHandshakeMessageHandler(new SocketsHttpHandler() { PooledConnectionLifetime = TimeSpan.FromMilliseconds(10 /* total guess */) });
        HttpClient client = new(clientHandler);

        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(DockerManifestV2));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(DockerContainerV1));

        client.DefaultRequestHeaders.Add("User-Agent", ".NET Container Library");

        return client;
    }

    public async Task Push(Image x, string name, string? tag, string baseName, Action<string> logProgressMessage)
    {
        tag ??= "latest";

        HttpClient client = GetClient();
        var reg = this;
        await Task.WhenAll(x.LayerDescriptors.Select(async descriptor => {
            string digest = descriptor.Digest;
            logProgressMessage($"Uploading layer {digest} to {reg.RegistryName}");
            if (await reg.BlobAlreadyUploaded(name, digest, client))
            {
                logProgressMessage($"Layer {digest} already existed");
                return;
            }

            // Blob wasn't there; can we tell the server to get it from the base image?
            HttpResponseMessage pushResponse = await client.PostAsync(new Uri(reg.BaseUri, $"/v2/{name}/blobs/uploads/?mount={digest}&from={baseName}"), content: null);

            if (pushResponse.StatusCode != HttpStatusCode.Created)
            {
                // The blob wasn't already available in another namespace, so fall back to explicitly uploading it

                if (!x.originatingRegistry.HasValue)
                {
                    throw new NotImplementedException("Need a good error for 'couldn't download a thing because no link to registry'");
                }

                // Ensure the blob is available locally
                await x.originatingRegistry.Value.DownloadBlob(x.OriginatingName, descriptor);
                // Then push it to the destination registry
                await reg.Push(Layer.FromDescriptor(descriptor), name, logProgressMessage);
                logProgressMessage($"Finished uploading layer {digest} to {reg.RegistryName}");
            }
        }));

        using (MemoryStream stringStream = new MemoryStream(Encoding.UTF8.GetBytes(x.config.ToJsonString())))
        {
            var configDigest = x.GetDigest(x.config);
            logProgressMessage($"Uploading config to registry at blob {configDigest}");
            await UploadBlob(name, configDigest, stringStream);
            logProgressMessage($"Uploaded config to registry");
        }

        var manifestDigest = x.GetDigest(x.manifest);
        logProgressMessage($"Uploading manifest to {RegistryName} as blob {manifestDigest}");
        string jsonString = x.manifest.ToJsonString();
        HttpContent manifestUploadContent = new StringContent(jsonString);
        manifestUploadContent.Headers.ContentType = new MediaTypeHeaderValue(DockerManifestV2);
        var putResponse = await client.PutAsync(new Uri(BaseUri, $"/v2/{name}/manifests/{manifestDigest}"), manifestUploadContent);

        if (!putResponse.IsSuccessStatusCode)
        {
            throw new ContainerHttpException("Registry push failed.", putResponse.RequestMessage?.RequestUri?.ToString(), jsonString);
        }
        logProgressMessage($"Uploaded manifest to {RegistryName}");

        logProgressMessage($"Uploading tag {tag} to {RegistryName}");
        var putResponse2 = await client.PutAsync(new Uri(BaseUri, $"/v2/{name}/manifests/{tag}"), manifestUploadContent);

        if (!putResponse2.IsSuccessStatusCode)
        {
            throw new ContainerHttpException("Registry push failed.", putResponse2.RequestMessage?.RequestUri?.ToString(), jsonString);
        }

        logProgressMessage($"Uploaded tag {tag} to {RegistryName}");
    }
}
