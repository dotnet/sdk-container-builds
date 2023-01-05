using System;
using System.Collections.Generic;
using Microsoft.NET.Build.Containers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Microsoft.NET.Build.Containers.Filesystem;

[TestClass]
public class RegistryTests
{
    [TestMethod]
    public async Task GetFromRegistry()
    {
        Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(DockerRegistryManager.LocalRegistry));

        Image downloadedImage = await registry.GetImageManifest(DockerRegistryManager.BaseImage, DockerRegistryManager.BaseImageTag);

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
