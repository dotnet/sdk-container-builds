// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Formats.Tar;
using Microsoft.NET.Build.Containers;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Globalization;

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

        Layer l = Layer.FromDirectory(directory: folder.Path, containerPath: "/app");

        Console.WriteLine(l.Descriptor);

        //Assert.AreEqual("application/vnd.oci.image.layer.v1.tar", l.Descriptor.MediaType); // TODO: configurability
        Assert.IsTrue(l.Descriptor.Size is >= 135 and <= 500, $"'l.Descriptor.Size' should be between 135 and 500, but is {l.Descriptor.Size}"); // TODO: determinism!
        //Assert.AreEqual("sha256:26140bc75f2fcb3bf5da7d3b531d995c93d192837e37df0eb5ca46e2db953124", l.Descriptor.Digest); // TODO: determinism!

        VerifyDescriptorInfo(l);

        var allEntries = LoadAllTarEntries(l.BackingFile);
        Assert.IsTrue(allEntries.TryGetValue("app/", out var appEntryType) && appEntryType == TarEntryType.Directory, "Missing app directory entry");
        Assert.IsTrue(allEntries.TryGetValue("app/TestFile.txt", out var fileEntryType) && fileEntryType == TarEntryType.RegularFile, "Missing TestFile.txt file entry");
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

        Layer l = Layer.FromFiles(new[]
        {
            (testFilePath,  "/app/TestFile.txt"),
            (testFilePath2, "/app/subfolder/TestFile.txt"),
        });

        Console.WriteLine(l.Descriptor);

        //Assert.AreEqual("application/vnd.oci.image.layer.v1.tar", l.Descriptor.MediaType); // TODO: configurability
        Assert.IsTrue(l.Descriptor.Size is >= 150 and <= 500, $"'l.Descriptor.Size' should be between 150 and 500, but is {l.Descriptor.Size}"); // TODO: determinism!
        //Assert.AreEqual("sha256:26140bc75f2fcb3bf5da7d3b531d995c93d192837e37df0eb5ca46e2db953124", l.Descriptor.Digest); // TODO: determinism!

        VerifyDescriptorInfo(l);
        
        var allEntries = LoadAllTarEntries(l.BackingFile);
        Assert.IsTrue(allEntries.TryGetValue("app/", out var appEntryType) && appEntryType == TarEntryType.Directory, "Missing app directory entry");
        Assert.IsTrue(allEntries.TryGetValue("app/TestFile.txt", out var fileEntryType) && fileEntryType == TarEntryType.RegularFile, "Missing TestFile.txt file entry");
        Assert.IsTrue(allEntries.TryGetValue("app/subfolder/", out var subfolderType) && subfolderType == TarEntryType.Directory, "Missing subfolder directory entry");
        Assert.IsTrue(allEntries.TryGetValue("app/subfolder/TestFile.txt", out var subfolderFileEntryType) && subfolderFileEntryType == TarEntryType.RegularFile, "Missing subfolder/TestFile.txt file entry");
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

        Assert.AreEqual(Convert.ToHexString(hashBytes), l.Descriptor.Digest.Substring("sha256:".Length), ignoreCase: true, CultureInfo.InvariantCulture);
        Assert.AreEqual(Convert.ToHexString(uncompressedHashBytes), l.Descriptor.UncompressedDigest?.Substring("sha256:".Length), ignoreCase: true, CultureInfo.InvariantCulture);
    }

    TransientTestFolder? testSpecificArtifactRoot;
    string? priorArtifactRoot;

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
    
    
    private static Dictionary<string, TarEntryType> LoadAllTarEntries(string file)
    {
        using var gzip = new GZipStream(File.OpenRead(file), CompressionMode.Decompress);
        using var tar = new TarReader(gzip);
        
        var entries = new Dictionary<string, TarEntryType>();
        
        TarEntry? entry;
        while ((entry = tar.GetNextEntry()) != null)
        {
            entries[entry.Name] = entry.EntryType;
        }

        return entries;
    }
}
