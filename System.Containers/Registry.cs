using Microsoft.VisualBasic;

using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace System.Containers;

public record struct Registry(Uri BaseUri)
{
    private const string DockerManifestV2 = "application/vnd.docker.distribution.manifest.v2+json";
    private const string DockerContainerV1 = "application/vnd.docker.container.image.v1+json";

    public async Task<Image> GetImageManifest(string name, string reference)
    {
        using HttpClient client = new(new HttpClientHandler() { UseDefaultCredentials = true });
        
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(DockerManifestV2));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(DockerContainerV1));

        client.DefaultRequestHeaders.Add("User-Agent", ".NET Container Library");

        var response = await client.GetAsync(new Uri(BaseUri, $"/v2/{name}/manifests/{reference}"));

        var s = await response.Content.ReadAsStringAsync();

        var manifest = JsonNode.Parse(s);

        Debug.Assert(manifest is not null);
        Debug.Assert(((string?)manifest["mediaType"]) == DockerManifestV2);

        JsonNode? config = manifest["config"];
        Debug.Assert(config is not null);
        Debug.Assert(((string?)config["mediaType"]) == DockerContainerV1);

        string? configSha = (string?)config["digest"];
        Debug.Assert(configSha is not null);

        response = await client.GetAsync(new Uri(BaseUri, $"/v2/{name}/blobs/{configSha}"));

        JsonNode? configDoc = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Debug.Assert(configDoc is not null);
        //Debug.Assert(((string?)configDoc["mediaType"]) == DockerContainerV1);

        return new Image(manifest, configDoc);
    }

    public async Task Push(Layer layer, string name)
    {
        using HttpClient client = new(new HttpClientHandler() { UseDefaultCredentials = true });

        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(DockerManifestV2));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(DockerContainerV1));

        client.DefaultRequestHeaders.Add("User-Agent", ".NET Container Library");

        HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, new Uri(BaseUri, $"/v2/{name}/blobs/{layer.Descriptor.Digest}")));

        if (response.StatusCode == Net.HttpStatusCode.OK)
        {
            // Already there!
            return;
        }

        HttpResponseMessage pushResponse = await client.PostAsync(new Uri(BaseUri,$"/v2/{name}/blobs/uploads/"), content: null);

        Debug.Assert(pushResponse.StatusCode == Net.HttpStatusCode.Accepted);

        //Uri uploadUri = new(BaseUri, pushResponse.Headers.GetValues("location").Single() + $"?digest={layer.Descriptor.Digest}");
        Debug.Assert(pushResponse.Headers.Location is not null);

        var x = new UriBuilder(pushResponse.Headers.Location);

        x.Query += $"&digest={Uri.EscapeDataString(layer.Descriptor.Digest)}";

        using (FileStream contents = File.OpenRead(layer.BackingFile))
        {
            // TODO: consider chunking
            StreamContent content = new StreamContent(contents);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Headers.ContentLength = contents.Length;
            HttpResponseMessage putResponse = await client.PutAsync(x.Uri, content);

            putResponse.Content.ToString();

            Debug.Assert(putResponse.IsSuccessStatusCode);
        }
    }

    public async Task Push(Image x, string name)
    {
        using HttpClient client = new(new HttpClientHandler() { UseDefaultCredentials = true });

        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(DockerManifestV2));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(DockerContainerV1));

        client.DefaultRequestHeaders.Add("User-Agent", ".NET Container Library");

        foreach (var layerJson in x.manifest["layers"].AsArray())
        {
            try
            {
                string digest = layerJson["digest"].ToString();
                HttpResponseMessage pushResponse = await client.PostAsync(new Uri(BaseUri, $"/v2/{name}/blobs/uploads/?mount={digest}&from={"dotnet/sdk" /* TODO */}"), content: null);
            }
            catch { }
        }

        HttpResponseMessage configResponse = await client.PostAsync(new Uri(BaseUri, $"/v2/{name}/blobs/uploads/?mount={x.manifest["config"]["digest"]}&from={"dotnet/sdk" /* TODO */}"), content: null);

        HttpContent manifestUploadContent = new StringContent(x.manifest.ToJsonString());
        manifestUploadContent.Headers.ContentType = new MediaTypeHeaderValue(DockerManifestV2);

        var putResponse = await client.PutAsync(new Uri(BaseUri, $"/v2/{name}/manifests/{x.GetSha()}"), manifestUploadContent);

        string putresponsestr = await putResponse.Content.ReadAsStringAsync();

        var putResponse2 = await client.PutAsync(new Uri(BaseUri, $"/v2/{name}/manifests/latest"), manifestUploadContent);

        Debug.Assert(putResponse.IsSuccessStatusCode);
    }
}