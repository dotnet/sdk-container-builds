using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace System.Containers;

public class Image
{
    internal JsonNode manifest;
    private JsonNode config;

    private List<Layer> newLayers = new();

    public Image(JsonNode manifest, JsonNode config)
    {
        this.manifest = manifest;
        this.config = config;
    }

    public void AddLayer(Layer l)
    {
        newLayers.Add(l);
        manifest["layers"].AsArray().Add(l.Descriptor);
    }

    public string GetSha()
    {
        using SHA256 mySHA256 = SHA256.Create();
        byte[] hash = mySHA256.ComputeHash(Encoding.UTF8.GetBytes(manifest.ToJsonString()));

        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
