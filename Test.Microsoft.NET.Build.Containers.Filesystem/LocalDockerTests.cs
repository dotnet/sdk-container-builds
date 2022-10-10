using Microsoft.NET.Build.Containers;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Test.Microsoft.NET.Build.Containers.Filesystem;

[TestClass]
public class LocalDockerTests
{
    [TestMethod]
    public void CanPullImageFromLocalDocker()
    {
        LocalDocker.Pull("mcr.microsoft.com/dotnet/runtime", "6.0");
    }
}