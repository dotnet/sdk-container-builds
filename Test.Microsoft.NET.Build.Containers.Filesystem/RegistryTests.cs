// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers;

namespace Test.Microsoft.NET.Build.Containers.Filesystem;

[TestClass]
public class RegistryTests
{
    [TestMethod]
    public async Task GetFromRegistry()
    {
        Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(DockerRegistryManager.LocalRegistry));

        string dotnetRoot = ToolsetUtils.GetDotNetPath();
        // TODO: The DOTNET_ROOT comes from the test host, but we have no idea what the SDK version is.
        var ridgraphfile = Path.Combine(dotnetRoot, "sdk", "7.0.100", "RuntimeIdentifierGraph.json");

        // Don't need rid graph for local registry image pulls - since we're only pushing single image manifests (not manifest lists)
        // as part of our setup, we could put literally anything in here. The file at the passed-in path would only get read when parsing manifests lists.
        ImageBuilder downloadedImage = await registry.GetImageManifest(DockerRegistryManager.BaseImage, DockerRegistryManager.Net6ImageTag, "linux-x64", ridgraphfile).ConfigureAwait(false);

        Assert.IsNotNull(downloadedImage);
    }

    [DataRow("public.ecr.aws", true)]
    [DataRow("123412341234.dkr.ecr.us-west-2.amazonaws.com", true)]
    [DataRow("123412341234.dkr.ecr-fips.us-west-2.amazonaws.com", true)]
    [DataRow("notvalid.dkr.ecr.us-west-2.amazonaws.com", false)]
    [DataRow("1111.dkr.ecr.us-west-2.amazonaws.com", false)]
    [DataRow("mcr.microsoft.com", false)]
    [DataRow("localhost", false)]
    [DataRow("hub", false)]
    [TestMethod]
    public void CheckIfAmazonECR(string registryName, bool isECR)
    {
        Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(registryName));
        Assert.AreEqual(isECR, registry.IsAmazonECRRegistry);
    }
}
