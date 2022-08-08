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
    [ClassInitialize]
    public static void InitRegistry(TestContext ctx) => DockerRegistryManager.StartAndPopulateDockerRegistry(ctx);

    [ClassCleanup]
    public static void TeardownRegistry() => DockerRegistryManager.ShutdownDockerRegistry();

    [TestMethod]
    public async Task GetFromRegistry()
    {
        Registry registry = new Registry(new Uri($"http://{DockerRegistryManager.LocalRegistry}"));

        Image downloadedImage = await registry.GetImageManifest(DockerRegistryManager.BaseImage, DockerRegistryManager.BaseImageTag);

        Assert.IsNotNull(downloadedImage);
    }
}
