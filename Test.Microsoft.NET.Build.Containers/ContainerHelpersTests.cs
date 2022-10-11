using Microsoft.NET.Build.Containers;
using System.Text;

namespace Test.Microsoft.NET.Build.Containers;

[TestClass]
public class ContainerHelpersTests
{
    [TestMethod]
    // Valid Tests
    [DataRow("mcr.microsoft.com", true)]
    [DataRow("mcr.microsoft.com:5001", true)] // Registries can have ports
    [DataRow("docker.io", true)] // default docker registry is considered valid

    // // Invalid tests
    [DataRow("mcr.mi-=crosoft.com", false)] // invalid url
    [DataRow("mcr.microsoft.com/", false)] // invalid url
    public void IsValidRegistry(string registry, bool expectedReturn)
    {
        Console.WriteLine($"Domain pattern is '{ReferenceParser.AnchoredDomainRegexp.ToString()}'");
        Assert.AreEqual(expectedReturn, ContainerHelpers.IsValidRegistry(registry));
    }

    [TestMethod]
    [DataRow("mcr.microsoft.com/dotnet/runtime:6.0", true, "mcr.microsoft.com", "dotnet/runtime", "6.0")]
    [DataRow("mcr.microsoft.com/dotnet/runtime", true, "mcr.microsoft.com", "dotnet/runtime", null)]
    [DataRow("mcr.microsoft.com/dotnet/runtime", true, "mcr.microsoft.com", "dotnet/runtime", null)]
    [DataRow("mcr.microsoft.com/", false, null, null, null)] // no image = nothing resolves
    // Ports tag along
    [DataRow("mcr.microsoft.com:54/dotnet/runtime", true, "mcr.microsoft.com:54", "dotnet/runtime", null)]
    // Even if nonsensical
    [DataRow("mcr.microsoft.com:0/dotnet/runtime", true, "mcr.microsoft.com:0", "dotnet/runtime", null)]
    // We don't allow hosts with missing ports when a port is anticipated
    [DataRow("mcr.microsoft.com:/dotnet/runtime", false, null, null, null)]
    // no image = nothing resolves
    [DataRow("mcr.microsoft.com/", false, null, null, null)]
    [DataRow("ubuntu:jammy", true, ContainerHelpers.DefaultRegistry, "ubuntu", "jammy")]
    public void TryParseFullyQualifiedContainerName(string fullyQualifiedName, bool expectedReturn, string expectedRegistry, string expectedImage, string expectedTag)
    {
        Assert.AreEqual(expectedReturn, ContainerHelpers.TryParseFullyQualifiedContainerName(fullyQualifiedName, out string? containerReg, out string? containerName, out string? containerTag, out string? containerDigest));
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
    [DataRow("80/tcp", true, 80, PortType.tcp, null)]
    [DataRow("80", true, 80, PortType.tcp, null)]
    [DataRow("125/dup", false, 125, PortType.tcp, ContainerHelpers.ParsePortError.InvalidPortType)]
    [DataRow("invalidNumber", false, null, null, ContainerHelpers.ParsePortError.InvalidPortNumber)]
    [DataRow("welp/unknowntype", false, null, null, (ContainerHelpers.ParsePortError)3)]
    [DataRow("a/b/c", false, null, null, ContainerHelpers.ParsePortError.UnknownPortFormat)]
    [DataRow("/tcp", false, null, null, ContainerHelpers.ParsePortError.MissingPortNumber)]
    public void CanParsePort(string input, bool shouldParse, int? expectedPortNumber, PortType? expectedType, ContainerHelpers.ParsePortError? expectedError) {
        var parseSuccess = ContainerHelpers.TryParsePort(input, out var port, out var errors);
        Assert.AreEqual<bool>(shouldParse, parseSuccess, $"{(shouldParse ? "Should" : "Shouldn't")} have parsed {input} into a port");

        if (shouldParse) {
            Assert.IsNotNull(port);
            Assert.AreEqual(port.number, expectedPortNumber);
            Assert.AreEqual(port.type, expectedType);
        } else {
            Assert.IsNull(port);
            Assert.IsNotNull(errors);
            Assert.AreEqual(expectedError, errors);
        }
    }
}
