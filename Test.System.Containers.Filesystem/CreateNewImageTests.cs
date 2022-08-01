using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Containers.Tasks;
using System.Diagnostics;
using System.IO;

#nullable disable

namespace Test.System.Containers.Tasks
{
    [TestClass]
    public class CreateNewImageTests
    {
        [TestMethod]
        public void Baseline()
        {
            DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "CreateNewImage_Baseline"));

            if (newProjectDir.Exists)
            {
                newProjectDir.Delete(recursive: true);
            }

            newProjectDir.Create();

            ProcessStartInfo info = new ProcessStartInfo
            {
                WorkingDirectory = newProjectDir.FullName,
                FileName = "dotnet",
                Arguments = "new console -f net7.0"
            };

            // Create the project to pack
            Process dotnetNew = Process.Start(info);
            Assert.IsNotNull(dotnetNew);
            dotnetNew.WaitForExit();
            Assert.AreEqual(0, dotnetNew.ExitCode);

            info.Arguments = "build --configuration release";

            Process dotnetPublish = Process.Start(info);
            Assert.IsNotNull(dotnetPublish);
            dotnetPublish.WaitForExit();
            Assert.AreEqual(0, dotnetPublish.ExitCode);

            CreateNewImage task = new CreateNewImage();
            task.BaseRegistry = "https://mcr.microsoft.com";
            task.BaseImageName = "dotnet/runtime";
            task.BaseImageTag = "6.0";

            task.OutputRegistryURL = "http://localhost:5010";
            task.PublishDirectory = newProjectDir.FullName + "\\bin\\release\\net7.0";
            task.NewImageName = "dotnet/testimage";
            task.WorkingDirectory = "app/";
            task.Entrypoint = "dotnet build";
            task.EntrypointArgs = "";

            Assert.IsTrue(task.Execute());
            newProjectDir.Delete(true);
        }

        [TestMethod]
        public void ParseContainerProperties_EndToEnd()
        {
            DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "ParseContainerProperties_EndToEnd"));

            if (newProjectDir.Exists)
            {
                newProjectDir.Delete(recursive: true);
            }

            newProjectDir.Create();

            ProcessStartInfo info = new ProcessStartInfo
            {
                WorkingDirectory = newProjectDir.FullName,
                FileName = "dotnet",
                Arguments = "new console -f net7.0",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            Process dotnetNew = Process.Start(info);
            Assert.IsNotNull(dotnetNew);
            dotnetNew.WaitForExit();
            Assert.AreEqual(0, dotnetNew.ExitCode);

            info.Arguments = "build --configuration release";

            Process dotnetPublish = Process.Start(info);
            Assert.IsNotNull(dotnetPublish);
            dotnetPublish.WaitForExit();
            Assert.AreEqual(0, dotnetPublish.ExitCode);

            ParseContainerProperties pcp = new ParseContainerProperties();
            pcp.ContainerBaseImage = "https://mcr.microsoft.com/dotnet/runtime:6.0";
            pcp.ContainerImageName = "dotnet/testimage";
            pcp.ContainerImageTag = "5.0";

            Assert.IsTrue(pcp.Execute());
            Assert.AreEqual("https://mcr.microsoft.com", pcp.ParsedContainerRegistry);
            Assert.AreEqual("dotnet/runtime", pcp.ParsedContainerImage);
            Assert.AreEqual("6.0", pcp.ParsedContainerTag);

            Assert.AreEqual("dotnet/testimage", pcp.NewImageName);
            Assert.AreEqual("5.0", pcp.NewImageTag);

            CreateNewImage cni = new CreateNewImage();
            cni.BaseRegistry = pcp.ParsedContainerRegistry;
            cni.BaseImageName = pcp.ParsedContainerImage;
            cni.BaseImageTag = pcp.ParsedContainerTag;
            cni.NewImageName = pcp.NewImageName;

            cni.OutputRegistryURL = "http://localhost:5010";
            cni.PublishDirectory = newProjectDir.FullName + "\\bin\\release\\net7.0";
            cni.WorkingDirectory = "app/";
            cni.Entrypoint = "ParseContainerProperties_EndToEnd";
            cni.EntrypointArgs = "";

            Assert.IsTrue(cni.Execute());
            newProjectDir.Delete(true);
        }
    }
}