using System.Containers;
using System.Text;

namespace Test.System.Containers
{
    [TestClass]
    public class ContainerHelpersTests
    {

        [TestMethod]
        [DataRow("https://mcr.microsoft.com/dotnet/runtime:6.0", true, "https://mcr.microsoft.com", "dotnet/runtime", "6.0")]
        [DataRow("https://mcr.microsoft.com/dotnet/runtime", true, "https://mcr.microsoft.com", "dotnet/runtime", "")]
        [DataRow("https://mcr.microsoft.com/", false, "", "", "")] // no image = nothing resolves
        [DataRow("docker://mcr.microsoft.com/dotnet/runtime", true, "docker://mcr.microsoft.com", "dotnet/runtime", "")]
        public void TryParseFullyQualifiedContainerName(string fullyQualifiedName, bool expectedReturn, string expectedRegistry, string expectedImage, string expectedTag)
        {
            Assert.AreEqual(ContainerHelpers.TryParseFullyQualifiedContainerName(fullyQualifiedName, out string containerReg, out string containerName, out string containerTag), expectedReturn);
            Assert.AreEqual(containerReg, expectedRegistry);
            Assert.AreEqual(containerName, expectedImage);
            Assert.AreEqual(containerTag, expectedTag);
        }

        [TestMethod]
        [DataRow("dotnet/runtime", true)]
        [DataRow("foo/bar", true)]
        [DataRow("-foo/bar", false)]
        [DataRow(".foo/bar", false)]
        [DataRow("_foo/bar", false)]
        [DataRow("foo/bar-", false)]
        [DataRow("foo/bar.", false)]
        [DataRow("foo/bar_", false)]
        public void IsValidImageName(string imageName, bool expectedReturn)
        {
            Assert.AreEqual(ContainerHelpers.IsValidImageName(imageName), expectedReturn);
        }

        [TestMethod]
        [DataRow("6.0", true)] // baseline
        [DataRow("5.2+asd123", true)] // with commit hash
        [DataRow(".6.0", false)] // starts with .
        [DataRow("-6.0", false)] // starts with -
        public void IsValidImageTag(string imageTag, bool expectedReturn)
        {
            Assert.AreEqual(ContainerHelpers.IsValidImageTag(imageTag), expectedReturn);
        }

        [TestMethod]
        public void IsValidImageTag_InvalidLength()
        {
            Assert.AreEqual(ContainerHelpers.IsValidImageTag(new string(' ', 129)), false);
        }
    }
}