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
        task.BaseRegistry = "mcr.microsoft.com";
        task.BaseImageName = "dotnet/runtime";
        task.BaseImageTag = "6.0";

        task.OutputRegistry = "localhost:5010";
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
        pcp.FullyQualifiedBaseImageName = "mcr.microsoft.com/dotnet/runtime:6.0";
        pcp.ContainerRegistry = "localhost:5010";
        pcp.ContainerImageName = "dotnet/testimage";
        pcp.ContainerImageTags = new [] {"5.0", "latest"};

        Assert.IsTrue(pcp.Execute());
        Assert.AreEqual("mcr.microsoft.com", pcp.ParsedContainerRegistry);
        Assert.AreEqual("dotnet/runtime", pcp.ParsedContainerImage);
        Assert.AreEqual("6.0", pcp.ParsedContainerTag);

        Assert.AreEqual("dotnet/testimage", pcp.NewContainerImageName);
        new []{ "5.0", "latest"}.SequenceEqual(pcp.NewContainerTags);

        CreateNewImage cni = new CreateNewImage();
        cni.BaseRegistry = pcp.ParsedContainerRegistry;
        cni.BaseImageName = pcp.ParsedContainerImage;
        cni.BaseImageTag = pcp.ParsedContainerTag;
        cni.ImageName = pcp.NewContainerImageName;
        cni.OutputRegistry = "localhost:5010";
        cni.PublishDirectory = Path.Combine(newProjectDir.FullName, "bin", "release", "net7.0");
        cni.WorkingDirectory = "app/";
        cni.Entrypoint = new TaskItem[] { new("ParseContainerProperties_EndToEnd") };
        cni.ImageTags = pcp.NewContainerTags;

        Assert.IsTrue(cni.Execute());
        newProjectDir.Delete(true);
    }

    /// <summary>
    /// Creates a console app that outputs the environment variable added to the image.
    /// </summary>
    [TestMethod]
    public void Tasks_EndToEnd_With_EnvironmentVariable_Validation()
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), nameof(Tasks_EndToEnd_With_EnvironmentVariable_Validation)));

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
            RedirectStandardError = true
        };

        Process dotnetNew = Process.Start(info);
        Assert.IsNotNull(dotnetNew);
        dotnetNew.WaitForExit();
        Assert.AreEqual(0, dotnetNew.ExitCode, dotnetNew.StandardOutput.ReadToEnd());

        File.WriteAllText(Path.Combine(newProjectDir.FullName, "Program.cs"), $"Console.Write(Environment.GetEnvironmentVariable(\"GoodEnvVar\"));");

        info.Arguments = "build --configuration release /p:runtimeidentifier=linux-x64";

        Process dotnetBuildRelease = Process.Start(info);
        Assert.IsNotNull(dotnetBuildRelease);
        dotnetBuildRelease.WaitForExit();
        dotnetBuildRelease.Kill();
        Assert.AreEqual(0, dotnetBuildRelease.ExitCode);

        ParseContainerProperties pcp = new ParseContainerProperties();
        pcp.FullyQualifiedBaseImageName = "mcr.microsoft.com/dotnet/runtime:6.0";
        pcp.ContainerRegistry = "";
        pcp.ContainerImageName = "dotnet/envvarvalidation";
        pcp.ContainerImageTag = "latest";

        Dictionary<string, string> dict = new Dictionary<string, string>();
        dict.Add("Value", "Foo");

        pcp.ContainerEnvironmentVariables = new[] { new TaskItem("B@dEnv.Var", dict), new TaskItem("GoodEnvVar", dict) };

        Assert.IsTrue(pcp.Execute());
        Assert.AreEqual("mcr.microsoft.com", pcp.ParsedContainerRegistry);
        Assert.AreEqual("dotnet/runtime", pcp.ParsedContainerImage);
        Assert.AreEqual("6.0", pcp.ParsedContainerTag);
        Assert.AreEqual(1, pcp.NewContainerEnvironmentVariables.Length);
        Assert.AreEqual("Foo", pcp.NewContainerEnvironmentVariables[0].GetMetadata("Value"));

        Assert.AreEqual("dotnet/envvarvalidation", pcp.NewContainerImageName);
        Assert.AreEqual("latest", pcp.NewContainerTags[0]);

        CreateNewImage cni = new CreateNewImage();
        cni.BaseRegistry = pcp.ParsedContainerRegistry;
        cni.BaseImageName = pcp.ParsedContainerImage;
        cni.BaseImageTag = pcp.ParsedContainerTag;
        cni.ImageName = pcp.NewContainerImageName;
        cni.OutputRegistry = pcp.NewContainerRegistry;
        cni.PublishDirectory = Path.Combine(newProjectDir.FullName, "bin", "release", "net7.0", "linux-x64");
        cni.WorkingDirectory = "/app";
        cni.Entrypoint = new TaskItem[] { new("/app/Tasks_EndToEnd_With_EnvironmentVariable_Validation") };
        cni.ImageTags = pcp.NewContainerTags;
        cni.ContainerEnvironmentVariables = pcp.NewContainerEnvironmentVariables;

        Assert.IsTrue(cni.Execute());

        ProcessStartInfo runInfo = new("docker", $"run --rm {pcp.NewContainerImageName}:latest")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        Process run = Process.Start(runInfo);
        Assert.IsNotNull(run);
        run.WaitForExit();
        Assert.AreEqual(0, run.ExitCode);
        Assert.AreEqual("Foo", run.StandardOutput.ReadToEnd());
    }
}
