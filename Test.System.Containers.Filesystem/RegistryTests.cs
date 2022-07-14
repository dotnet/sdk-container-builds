using System;
using System.Collections.Generic;
using System.Containers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.System.Containers.Filesystem;

[TestClass]
public class RegistryTests
{
    [TestMethod]
    public async Task GetFromRegistry()
    {
        Registry registry = new Registry(new Uri($"http://{DockerRegistryManager.LocalRegistry}"));

        Image downloadedImage = await registry.GetImageManifest(DockerRegistryManager.BaseImage, DockerRegistryManager.BaseImageTag);

        Assert.IsNotNull(downloadedImage);
    }
}
