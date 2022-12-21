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

        // TODO: The DOTNET_ROOT comes from the test host, but we have no idea what the SDK version is.
        var ridgraphfile = Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT"), "sdk", "7.0.100", "RuntimeIdentifierGraph.json");

        Image downloadedImage = await registry.GetImageManifest(DockerRegistryManager.BaseImage, DockerRegistryManager.Net6ImageTag, "linux-x64", ridgraphfile); // don't need rid graph for local registry

        Assert.IsNotNull(downloadedImage);
    }
}
