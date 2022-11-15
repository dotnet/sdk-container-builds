using Microsoft.NET.Build.Containers;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Test.Microsoft.NET.Build.Containers.Filesystem;

[TestClass]
public class LayerEndToEnd
{
    [TestMethod]
    public void SingleFileInFolder()
    {
        using TransientTestFolder folder = new();

        string testFilePath = Path.Join(folder.Path, "TestFile.txt");
        string testString = $"Test content for {nameof(SingleFileInFolder)}";

        File.WriteAllText(testFilePath, testString);

        Layer l = Layer.FromDirectory(directory: folder.Path, containerUser: "root", containerPath: "/app");

        Console.WriteLine(l.Descriptor);

        //Assert.AreEqual("application/vnd.oci.image.layer.v1.tar", l.Descriptor.MediaType); // TODO: configurability
        Assert.IsTrue(l.Descriptor.Size is >= 135 and <= 200, $"'l.Descriptor.Size' should be between 135 and 200, but is {l.Descriptor.Size}"); // TODO: determinism!
        //Assert.AreEqual("sha256:26140bc75f2fcb3bf5da7d3b531d995c93d192837e37df0eb5ca46e2db953124", l.Descriptor.Digest); // TODO: determinism!

        VerifyDescriptorInfo(l);
    }

    [TestMethod]
    public void TwoFilesInTwoFolders()
    {
        using TransientTestFolder folder = new();

        string testFilePath = Path.Join(folder.Path, "TestFile.txt");
        string testString = $"Test content for {nameof(TwoFilesInTwoFolders)}";
        File.WriteAllText(testFilePath, testString);

        using TransientTestFolder folder2 = new();
        string testFilePath2 = Path.Join(folder2.Path, "TestFile.txt");
        string testString2 = $"Test content 2 for {nameof(TwoFilesInTwoFolders)}";
        File.WriteAllText(testFilePath2, testString2);

        Layer l = Layer.FromFiles("/app", "root",
        new[]
        {
            (testFilePath,  "TestFile.txt"),
            (testFilePath2, "subfolder/TestFile.txt"),
        });

        Console.WriteLine(l.Descriptor);

        //Assert.AreEqual("application/vnd.oci.image.layer.v1.tar", l.Descriptor.MediaType); // TODO: configurability
        Assert.IsTrue(l.Descriptor.Size is >= 150 and <= 200, $"'l.Descriptor.Size' should be between 150 and 200, but is {l.Descriptor.Size}"); // TODO: determinism!
        //Assert.AreEqual("sha256:26140bc75f2fcb3bf5da7d3b531d995c93d192837e37df0eb5ca46e2db953124", l.Descriptor.Digest); // TODO: determinism!

        VerifyDescriptorInfo(l);
    }

    private static void VerifyDescriptorInfo(Layer l)
    {
        Assert.AreEqual(l.Descriptor.Size, new FileInfo(l.BackingFile).Length);

        byte[] hashBytes;
        byte[] uncompressedHashBytes;

        using (FileStream fs = File.OpenRead(l.BackingFile))
        {
            hashBytes = SHA256.HashData(fs);

            fs.Position = 0;

            using (GZipStream decompressionStream = new GZipStream(fs, CompressionMode.Decompress))
            {
                uncompressedHashBytes = SHA256.HashData(decompressionStream);
            }
        }

        Assert.AreEqual(Convert.ToHexString(hashBytes), l.Descriptor.Digest.Substring("sha256:".Length), ignoreCase: true);
        Assert.AreEqual(Convert.ToHexString(uncompressedHashBytes), l.Descriptor.UncompressedDigest.Substring("sha256:".Length), ignoreCase: true);
    }

    TransientTestFolder testSpecificArtifactRoot;
    string priorArtifactRoot;

    [TestInitialize]
    public void TestInitialize()
    {
        testSpecificArtifactRoot = new();

        priorArtifactRoot = ContentStore.ArtifactRoot;

        ContentStore.ArtifactRoot = testSpecificArtifactRoot.Path;
    }

    [TestCleanup]
    public void TestCleanup()
    {
        testSpecificArtifactRoot?.Dispose();
        if (priorArtifactRoot is not null)
        {
            ContentStore.ArtifactRoot = priorArtifactRoot;
        }
    }
}
