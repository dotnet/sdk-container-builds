using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.NET.Build.Containers.Tasks;

#nullable disable

namespace Test.Microsoft.NET.Build.Containers.Tasks
{
    [TestClass]
    public class ParseContainerPropertiesTests
    {
        [TestMethod]
        public void Baseline()
        {
            ParseContainerProperties task = new ParseContainerProperties();
            task.FullyQualifiedBaseImageName = "mcr.microsoft.com/dotnet/runtime:6.0";
            task.ContainerRegistry = "localhost:5010";
            task.ContainerImageName = "dotnet/testimage";
            task.ContainerImageTags = new[] { "5.0" };

            Assert.IsTrue(task.Execute());
            Assert.AreEqual("mcr.microsoft.com", task.ParsedContainerRegistry);
            Assert.AreEqual("dotnet/runtime", task.ParsedContainerImage);
            Assert.AreEqual("6.0", task.ParsedContainerTag);

            Assert.AreEqual("dotnet/testimage", task.NewContainerImageName);
            new[] { "5.0" }.SequenceEqual(task.NewContainerTags);
        }

        [TestMethod]
        public void BaseRegistriesWithNoSchemeGetHttps()
        {
            ParseContainerProperties task = new ParseContainerProperties();
            task.FullyQualifiedBaseImageName = "mcr.microsoft.com/dotnet/runtime:6.0";
            task.ContainerRegistry = "localhost:5010";
            task.ContainerImageName = "dotnet/testimage";
            task.ContainerImageTags = new[] { "5.0" };

            Assert.IsTrue(task.Execute());
            Assert.AreEqual("mcr.microsoft.com", task.ParsedContainerRegistry);
            Assert.AreEqual("dotnet/runtime", task.ParsedContainerImage);
            Assert.AreEqual("6.0", task.ParsedContainerTag);

            Assert.AreEqual("localhost:5010", task.NewContainerRegistry);
            Assert.AreEqual("dotnet/testimage", task.NewContainerImageName);
            new[] { "5.0" }.SequenceEqual(task.NewContainerTags);
        }

        [TestMethod]
        public void UserRegistriesWithNoSchemeGetHttps()
        {
            ParseContainerProperties task = new ParseContainerProperties();
            task.FullyQualifiedBaseImageName = "mcr.microsoft.com/dotnet/runtime:6.0";
            task.ContainerRegistry = "localhost:5010";
            task.ContainerImageName = "dotnet/testimage";
            task.ContainerImageTags = new[] { "5.0" };

            Assert.IsTrue(task.Execute());
            Assert.AreEqual("mcr.microsoft.com", task.ParsedContainerRegistry);
            Assert.AreEqual("dotnet/runtime", task.ParsedContainerImage);
            Assert.AreEqual("6.0", task.ParsedContainerTag);

            Assert.AreEqual("localhost:5010", task.NewContainerRegistry);
            Assert.AreEqual("dotnet/testimage", task.NewContainerImageName);
            new[] { "5.0" }.SequenceEqual(task.NewContainerTags);
        }

        [TestMethod]
        public void SpacesGetReplacedWithDashes()
        {
            ParseContainerProperties task = new ParseContainerProperties();
            task.FullyQualifiedBaseImageName = "mcr microsoft com/dotnet runtime:6 0";
            task.ContainerRegistry = "localhost:5010";

            // Spaces in the "new" container info don't pass the regex.
            task.ContainerImageName = "dotnet/testimage";
            task.ContainerImageTags = new[] { "5.0" };

            Assert.IsTrue(task.Execute());
            Assert.AreEqual("mcr-microsoft-com", task.ParsedContainerRegistry);
            Assert.AreEqual("dotnet-runtime", task.ParsedContainerImage);
            Assert.AreEqual("6-0", task.ParsedContainerTag);

            Assert.AreEqual("dotnet/testimage", task.NewContainerImageName);
            new[] { "5.0" }.SequenceEqual(task.NewContainerTags);
        }

        [TestMethod]
        [Ignore("Task logging in tests unsupported.")]
        public void RegexCatchesInvalidContainerNames()
        {
            ParseContainerProperties task = new ParseContainerProperties();
            task.FullyQualifiedBaseImageName = "mcr.microsoft.com/dotnet/runtime:6 0";
            task.ContainerRegistry = "localhost:5010";

            // Spaces in the "new" container info don't pass the regex.
            task.ContainerImageName = "dotnet testimage";
            task.ContainerImageTags = new[] { "5.0" };

            Assert.IsFalse(task.Execute());
            // To do: Verify output contains expected error
        }

        [TestMethod]
        [Ignore("Task logging in tests unsupported.")]
        public void RegexCatchesInvalidContainerTags()
        {
            ParseContainerProperties task = new ParseContainerProperties();
            task.FullyQualifiedBaseImageName = "mcr.microsoft.com/dotnet/runtime:6 0";
            task.ContainerRegistry = "localhost:5010";
            // Spaces in the "new" container info don't pass the regex.
            task.ContainerImageName = "dotnet/testimage";
            task.ContainerImageTags = new[] { "5.0" };

            Assert.IsFalse(task.Execute());
            // To do: Verify output contains expected error
        }

        [TestMethod]
        [Ignore("Task logging in tests unsupported.")]
        public void CanOnlySupplyOneOfTagAndTags()
        {
            ParseContainerProperties task = new ParseContainerProperties();
            task.FullyQualifiedBaseImageName = "mcr.microsoft.com/dotnet/runtime:6 0";
            task.ContainerRegistry = "localhost:5010";
            // Spaces in the "new" container info don't pass the regex.
            task.ContainerImageName = "dotnet/testimage";
            task.ContainerImageTag = "a.b";
            task.ContainerImageTags = new[] { "5.0" };

            Assert.IsFalse(task.Execute());
            // To do: Verify output contains expected error
        }
    }
}