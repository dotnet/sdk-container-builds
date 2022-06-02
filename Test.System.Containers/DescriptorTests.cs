using NJsonSchema;
using NJsonSchema.Validation;

using System.Collections;
using System.Collections.Generic;
using System.Containers;
using System.Text.Json;

namespace Test.System.Containers;

[TestClass]
public class DescriptorTests
{
    [TestMethod]
    public async Task BasicConstructorAsync()
    {
        Descriptor d = new(
            mediaType: "application/vnd.oci.image.manifest.v1+json",
            digest: "sha256:5b0bcabd1ed22e9fb1310cf6c2dec7cdef19f0ad69efa1f392e94a4333501270",
            size: 7682);

        string json = JsonSerializer.Serialize(d, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);

        Assert.AreEqual("application/vnd.oci.image.manifest.v1+json", d.MediaType);
        Assert.AreEqual("sha256:5b0bcabd1ed22e9fb1310cf6c2dec7cdef19f0ad69efa1f392e94a4333501270", d.Digest);
        Assert.AreEqual(7_682, d.Size);

        Assert.IsNull(d.Annotations);
        Assert.IsNull(d.Data);
        Assert.IsNull(d.Urls);

        JsonSchema schema = await JsonSchema.FromJsonAsync(await File.ReadAllTextAsync(Path.Combine("schema/content-descriptor.json")), Path.Combine("schema/content-descriptor.json"));

        ICollection<ValidationError> errors = schema.Validate(json);
        Assert.AreEqual(0, errors.Count);
    }
}
