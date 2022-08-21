using Microsoft.NET.Build.Containers;
using System.Text;

namespace Test.Microsoft.NET.Build.Containers;

[TestClass]
public class ContainerHelpersTests
{
    [TestMethod]
    // Valid Tests
    [DataRow("https://mcr.microsoft.com", true)]
    [DataRow("https://mcr.microsoft.com/", true)]
    [DataRow("http://mcr.microsoft.com:5001", true)] // Registries can have ports
    [DataRow("docker://mcr.microsoft.com:5001", true)] // docker:// is considered valid

    // // Invalid tests
    [DataRow("docker://mcr.microsoft.com:xyz/dotnet/runtime:6.0", false)] // invalid port
    [DataRow("httpz://mcr.microsoft.com", false)] // invalid scheme
    [DataRow("https://mcr.mi-=crosoft.com", false)] // invalid url
    [DataRow("mcr.microsoft.com/", false)] // Missing scheme
    public void IsValidRegistry(string registry, bool expectedReturn)
    {
        Assert.AreEqual(expectedReturn, ContainerHelpers.IsValidRegistry(registry));
    }

    [TestMethod]
    [DataRow("https://mcr.microsoft.com/dotnet/runtime:6.0", true, "https://mcr.microsoft.com", "dotnet/runtime", "6.0")]
    [DataRow("https://mcr.microsoft.com/dotnet/runtime", true, "https://mcr.microsoft.com", "dotnet/runtime", "")]
    [DataRow("docker://mcr.microsoft.com/dotnet/runtime", true, "docker://mcr.microsoft.com", "dotnet/runtime", "")]
    [DataRow("https://mcr.microsoft.com/", false, null, null, null)] // no image = nothing resolves
    // Ports tag along
    [DataRow("docker://mcr.microsoft.com:54/dotnet/runtime", true, "docker://mcr.microsoft.com:54", "dotnet/runtime", "")]
    // Unless they're invalid
    [DataRow("docker://mcr.microsoft.com:0/dotnet/runtime", true, "docker://mcr.microsoft.com", "dotnet/runtime", "")]
    // Strip the ':' in an unspecified port
    [DataRow("docker://mcr.microsoft.com:/dotnet/runtime", true, "docker://mcr.microsoft.com", "dotnet/runtime", "")]
    // no image = nothing resolves
    [DataRow("https://mcr.microsoft.com/", false, null, null, null)]
    public void TryParseFullyQualifiedContainerName(string fullyQualifiedName, bool expectedReturn, string expectedRegistry, string expectedImage, string expectedTag)
    {
        Assert.AreEqual(expectedReturn, ContainerHelpers.TryParseFullyQualifiedContainerName(fullyQualifiedName, out string? containerReg, out string? containerName, out string? containerTag));
        Assert.AreEqual(expectedRegistry, containerReg);
        Assert.AreEqual(expectedImage, containerName);
        Assert.AreEqual(expectedTag, containerTag);
    }

    [TestMethod]
    [DataRow("dotnet/runtime", true)]
    [DataRow("foo/bar", true)]
    [DataRow("registry", true)]
    [DataRow("-foo/bar", false)]
    [DataRow(".foo/bar", false)]
    [DataRow("_foo/bar", false)]
    [DataRow("foo/bar-", false)]
    [DataRow("foo/bar.", false)]
    [DataRow("foo/bar_", false)]
    public void IsValidImageName(string imageName, bool expectedReturn)
    {
        Assert.AreEqual(expectedReturn, ContainerHelpers.IsValidImageName(imageName));
    }

    [TestMethod]
    [DataRow("6.0", true)] // baseline
    [DataRow("5.2-asd123", true)] // with commit hash
    [DataRow(".6.0", false)] // starts with .
    [DataRow("-6.0", false)] // starts with -
    [DataRow("---", false)] // malformed
    public void IsValidImageTag(string imageTag, bool expectedReturn)
    {
        Assert.AreEqual(expectedReturn, ContainerHelpers.IsValidImageTag(imageTag));
    }

    [TestMethod]
    public void IsValidImageTag_InvalidLength()
    {
        Assert.AreEqual(false, ContainerHelpers.IsValidImageTag(new string('a', 129)));
    }

    [TestMethod]
    [DataRow("80/tcp", true, 80, PortType.tcp)]
    [DataRow("80", true, 80, PortType.tcp)]
    [DataRow("125/dup", false, 125, PortType.tcp)]
    [DataRow("invalidNumber", false, null, null)]
    [DataRow("80/unknowntype", false, null, null)]
    public void CanParsePort(string input, bool shouldParse, int? expectedPortNumber, PortType? expectedType) {
        var parseSuccess = ContainerHelpers.TryParsePort(input, out var parsedPort);
        Assert.AreEqual(shouldParse, parseSuccess, $"Should have parsed {input} into a port");
        if (!shouldParse) {
            Assert.IsNull(parsedPort);
        }
        if (shouldParse) {
            Assert.IsNotNull(parsedPort);
            Assert.AreEqual(parsedPort.number, expectedPortNumber);
            Assert.AreEqual(parsedPort.type, expectedType);
        }
    }
}
