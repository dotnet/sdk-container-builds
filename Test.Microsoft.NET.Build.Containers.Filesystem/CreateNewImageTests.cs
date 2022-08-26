using System.Collections;
using Microsoft.NET.Build.Containers.Tasks;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Test.Microsoft.NET.Build.Containers.Tasks;

[TestClass]
public class CreateNewImageTests
{
    [TestMethod]
    public void CreateNewImage_Baseline()
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), nameof(CreateNewImage_Baseline)));

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

        task.OutputRegistry = "http://localhost:5010";
        task.PublishDirectory = Path.Combine(newProjectDir.FullName, "bin", "release", "net7.0");
        task.ImageName = "dotnet/testimage";
        task.WorkingDirectory = "app/";
        task.Entrypoint = new TaskItem[] { new("dotnet"), new("build") };

        Assert.IsTrue(task.Execute());
        newProjectDir.Delete(true);
    }

    [TestMethod]
    public void ParseContainerProperties_EndToEnd()
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), nameof(ParseContainerProperties_EndToEnd)));

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
        pcp.FullyQualifiedBaseImageName = "https://mcr.microsoft.com/dotnet/runtime:6.0";
        pcp.ContainerRegistry = "http://localhost:5010";
        pcp.ContainerImageName = "dotnet/testimage";
        pcp.ContainerImageTag = "5.0;latest";

        Assert.IsTrue(pcp.Execute());
        Assert.AreEqual("https://mcr.microsoft.com", pcp.ParsedContainerRegistry);
        Assert.AreEqual("dotnet/runtime", pcp.ParsedContainerImage);
        Assert.AreEqual("6.0", pcp.ParsedContainerTag);

        Assert.AreEqual("dotnet/testimage", pcp.NewContainerImageName);
        new []{ "5.0", "latest"}.SequenceEqual(pcp.NewContainerTags);

        CreateNewImage cni = new CreateNewImage();
        cni.BaseRegistry = pcp.ParsedContainerRegistry;
        cni.BaseImageName = pcp.ParsedContainerImage;
        cni.BaseImageTag = pcp.ParsedContainerTag;
        cni.ImageName = pcp.NewContainerImageName;
        cni.OutputRegistry = "http://localhost:5010";
        cni.PublishDirectory = Path.Combine(newProjectDir.FullName, "bin", "release", "net7.0");
        cni.WorkingDirectory = "app/";
        cni.Entrypoint = new TaskItem[] { new("ParseContainerProperties_EndToEnd") };
        cni.ImageTags = pcp.NewContainerTags;

        Assert.IsTrue(cni.Execute());
        newProjectDir.Delete(true);
    }
}
