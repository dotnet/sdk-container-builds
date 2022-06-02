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
        Registry registry = new Registry(new Uri("http://localhost:5000"));

        var x = await registry.GetImageManifest("dotnet/sdk", "6.0");
    }
}
