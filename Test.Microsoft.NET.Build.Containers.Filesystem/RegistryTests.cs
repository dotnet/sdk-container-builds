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
}
