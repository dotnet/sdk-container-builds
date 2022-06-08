using System.Text.Json.Nodes;

namespace System.Containers;

public class Image
{
    private JsonNode manifest;
    private JsonNode config;

    private List<Layer> newLayers = new();

    public Image(JsonNode manifest, JsonNode config)
    {
        this.manifest = manifest;
        this.config = config;
    }

    void AddLayer(Layer l)
    {
        newLayers.Add(l);
    }
}
