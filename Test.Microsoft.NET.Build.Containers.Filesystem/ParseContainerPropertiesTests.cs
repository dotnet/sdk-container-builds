using static Microsoft.NET.Build.Containers.KnownStrings;
using static Microsoft.NET.Build.Containers.KnownStrings.Properties;

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
            task.ContainerImageTags = new[] { "5.0", "latest" };

            Assert.IsTrue(task.Execute());
            Assert.AreEqual("mcr.microsoft.com", task.ParsedContainerRegistry);
            Assert.AreEqual("dotnet/runtime", task.ParsedContainerImage);
            Assert.AreEqual("6.0", task.ParsedContainerTag);

            Assert.AreEqual("dotnet/testimage", task.NewContainerImageName);
            CollectionAssert.AreEquivalent(new[] { "5.0", "latest" }, task.NewContainerTags);
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
            CollectionAssert.AreEquivalent(new[] { "5.0" }, task.NewContainerTags);
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
            CollectionAssert.AreEquivalent(new[] { "5.0" }, task.NewContainerTags);
        }

        [TestMethod]
        public void SpacesGetReplacedWithDashes()
        {
             var (project, _) = Evaluator.InitProject(new () {
                [ContainerBaseImage] = "mcr microsoft com/dotnet runtime:6.0",
                [ContainerRegistry] = "localhost:5010"
            });

            var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
            Assert.IsTrue(instance.Build(new[]{ComputeContainerConfig}, null, null, out var outputs));

            Assert.AreEqual("mcr-microsoft-com", instance.GetPropertyValue(ContainerBaseRegistry));
            Assert.AreEqual("dotnet-runtime", instance.GetPropertyValue(ContainerBaseName));
            Assert.AreEqual("6.0", instance.GetPropertyValue(ContainerBaseTag));
        }

        [TestMethod]
        public void RegexCatchesInvalidContainerNames()
        {
             var (project, logs) = Evaluator.InitProject(new () {
                [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:6.0",
                [ContainerRegistry] = "localhost:5010",
                [ContainerImageName] = "dotnet testimage",
                [ContainerImageTag] = "5.0"
            }, captureLogs: true);
            
            var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
            Assert.IsTrue(instance.Build(new[]{ComputeContainerConfig}, new [] { logs }, null, out var outputs));
            Assert.IsTrue(logs.Warnings.Count > 0);
            Assert.AreEqual(logs.Warnings[0].Code, ErrorCodes.CONTAINER001);
        }

        [TestMethod]
        public void RegexCatchesInvalidContainerTags()
        {
            var (project, logs) = Evaluator.InitProject(new () {
                [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:6.0",
                [ContainerRegistry] = "localhost:5010",
                [ContainerImageName] = "dotnet/testimage",
                [ContainerImageTag] = "5 0"
            }, captureLogs: true);

            var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
            Assert.IsFalse(instance.Build(new[]{ComputeContainerConfig},  new [] { logs }, null, out var outputs));

            Assert.IsTrue(logs.Errors.Count > 0);
            Assert.AreEqual(logs.Errors[0].Code, ErrorCodes.CONTAINER004);
        }

        [TestMethod]
        public void CanOnlySupplyOneOfTagAndTags()
        {
            var (project, logs) = Evaluator.InitProject(new () {
                [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:6.0",
                [ContainerRegistry] = "localhost:5010",
                [ContainerImageName] = "dotnet/testimage",
                [ContainerImageTag] = "5.0",
                [ContainerImageTags] = "latest;oldest"
            }, captureLogs: true);

            var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
            Assert.IsFalse(instance.Build(new[]{ComputeContainerConfig},  new [] { logs }, null, out var outputs));

            Assert.IsTrue(logs.Errors.Count > 0);
            Assert.AreEqual(logs.Errors[0].Code, ErrorCodes.CONTAINER005);
        }
    }
}