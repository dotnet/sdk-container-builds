using Microsoft.NET.Build.Containers;
using System.Text;

namespace Test.Microsoft.NET.Build.Containers;

[TestClass]
public class ContainerHelpersTests
{

    [TestMethod]
    [DataRow("https://mcr.microsoft.com/dotnet/runtime:6.0", true, "https://mcr.microsoft.com", "dotnet/runtime", "6.0")]
    [DataRow("https://mcr.microsoft.com/dotnet/runtime", true, "https://mcr.microsoft.com", "dotnet/runtime", "")]
    [DataRow("docker://mcr.microsoft.com/dotnet/runtime", true, "docker://mcr.microsoft.com", "dotnet/runtime", "")]
    [DataRow("https://mcr.microsoft.com/", false, null, null, null)] // no image = nothing resolves
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
}
