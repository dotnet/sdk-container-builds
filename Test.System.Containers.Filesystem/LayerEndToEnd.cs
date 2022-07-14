using System.Containers;
using System.Security.Cryptography;

namespace Test.System.Containers.Filesystem;

[TestClass]
public class LayerEndToEnd
{
    [TestMethod]
    public void SingleFile()
    {
        using TransientTestFolder folder = new();

        string testFilePath = Path.Join(folder.Path, "TestFile.txt");
        string testString = $"Test content for {nameof(SingleFile)}";

        File.WriteAllText(testFilePath, testString);

        Layer l = Layer.FromDirectory(directory: folder.Path, containerPath: "/app");

        Console.WriteLine(l.Descriptor);

        //Assert.AreEqual("application/vnd.oci.image.layer.v1.tar", l.Descriptor.MediaType); // TODO: configurability
        Assert.AreEqual(2048, l.Descriptor.Size);
        //Assert.AreEqual("sha256:26140bc75f2fcb3bf5da7d3b531d995c93d192837e37df0eb5ca46e2db953124", l.Descriptor.Digest); // TODO: determinism!

        Assert.AreEqual(l.Descriptor.Size, new FileInfo(l.BackingFile).Length);

        byte[] hashBytes;

        using (SHA256 hasher = SHA256.Create())
        using (FileStream fs = File.OpenRead(l.BackingFile))
        {
            hashBytes = hasher.ComputeHash(fs);
        }

        Assert.AreEqual(Convert.ToHexString(hashBytes), l.Descriptor.Digest.Substring("sha256:".Length), ignoreCase: true);
    }

    TransientTestFolder? testSpecificArtifactRoot;
    string? priorArtifactRoot;

    [TestInitialize]
    public void TestInitialize()
    {
        testSpecificArtifactRoot = new();

        priorArtifactRoot = Configuration.ArtifactRoot;

        Configuration.ArtifactRoot = testSpecificArtifactRoot.Path;
    }

    [TestCleanup]
    public void TestCleanup()
    {
        testSpecificArtifactRoot?.Dispose();
        if (priorArtifactRoot is not null)
        {
            Configuration.ArtifactRoot = priorArtifactRoot;
        }
    }
}
