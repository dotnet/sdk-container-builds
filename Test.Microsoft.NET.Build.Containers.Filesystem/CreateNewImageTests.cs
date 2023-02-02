// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.CommandUtils;
using Test.Microsoft.NET.Build.Containers.Filesystem;

namespace Test.Microsoft.NET.Build.Containers.Tasks;

[TestClass]
public class CreateNewImageTests
{
    private TestContext? testContextInstance;

    /// <summary>
    ///Gets or sets the test context which provides
    ///information about and functionality for the current test run.
    ///</summary>
    public TestContext TestContext
    {
        get
        {
            return testContextInstance ?? throw new InvalidOperationException($"{nameof(TestContext)} is null.");
        }
        set
        {
            testContextInstance = value;
        }
    }

    public static string RuntimeGraphFilePath()
    {
        string dotnetRoot = ToolsetUtils.GetDotNetPath();
        DirectoryInfo sdksDir = new(Path.Combine(dotnetRoot, "sdk"));

        var lastWrittenSdk = sdksDir.EnumerateDirectories().OrderByDescending(di => di.LastWriteTime).First();

        return lastWrittenSdk.GetFiles("RuntimeIdentifierGraph.json").Single().FullName;
    }

    [TestMethod]
    public void CreateNewImage_Baseline()
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, nameof(CreateNewImage_Baseline)));

        if (newProjectDir.Exists)
        {
            newProjectDir.Delete(recursive: true);
        }

        newProjectDir.Create();

        new DotnetCommand(TestContext, "new", "console", "-f", "net7.0")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        new DotnetCommand(TestContext, "publish", "-c", "Release", "-r", "linux-arm64", "--no-self-contained")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        CreateNewImage task = new CreateNewImage();
        task.BaseRegistry = "mcr.microsoft.com";
        task.BaseImageName = "dotnet/runtime";
        task.BaseImageTag = "7.0";

        task.OutputRegistry = "localhost:5010";
        task.PublishDirectory = Path.Combine(newProjectDir.FullName, "bin", "Release", "net7.0", "linux-arm64", "publish");
        task.ImageName = "dotnet/testimage";
        task.ImageTags = new[] { "latest" };
        task.WorkingDirectory = "app/";
        task.ContainerRuntimeIdentifier = "linux-arm64";
        task.Entrypoint = new TaskItem[] { new("dotnet"), new("build") };
        task.RuntimeIdentifierGraphPath = RuntimeGraphFilePath();

        Assert.IsTrue(task.Execute());
        newProjectDir.Delete(true);
    }

    [TestMethod]
    public void ParseContainerProperties_EndToEnd()
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, nameof(ParseContainerProperties_EndToEnd)));

        if (newProjectDir.Exists)
        {
            newProjectDir.Delete(recursive: true);
        }

        newProjectDir.Create();

        new DotnetCommand(TestContext, "new", "console", "-f", "net7.0")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        new DotnetCommand(TestContext, "build", "--configuration", "release")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        ParseContainerProperties pcp = new ParseContainerProperties();
        pcp.FullyQualifiedBaseImageName = "mcr.microsoft.com/dotnet/runtime:7.0";
        pcp.ContainerRegistry = "localhost:5010";
        pcp.ContainerImageName = "dotnet/testimage";
        pcp.ContainerImageTags = new[] { "5.0", "latest" };

        Assert.IsTrue(pcp.Execute());
        Assert.AreEqual("mcr.microsoft.com", pcp.ParsedContainerRegistry);
        Assert.AreEqual("dotnet/runtime", pcp.ParsedContainerImage);
        Assert.AreEqual("7.0", pcp.ParsedContainerTag);

        Assert.AreEqual("dotnet/testimage", pcp.NewContainerImageName);
        CollectionAssert.AreEquivalent(new[] { "5.0", "latest" }, pcp.NewContainerTags);

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
        cni.ContainerRuntimeIdentifier = "linux-x64";
        cni.RuntimeIdentifierGraphPath = RuntimeGraphFilePath();

        Assert.IsTrue(cni.Execute());
        newProjectDir.Delete(true);
    }

    /// <summary>
    /// Creates a console app that outputs the environment variable added to the image.
    /// </summary>
    [TestMethod]
    public void Tasks_EndToEnd_With_EnvironmentVariable_Validation()
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, nameof(Tasks_EndToEnd_With_EnvironmentVariable_Validation)));

        if (newProjectDir.Exists)
        {
            newProjectDir.Delete(recursive: true);
        }

        newProjectDir.Create();

        new DotnetCommand(TestContext, "new", "console", "-f", "net7.0")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        File.WriteAllText(Path.Combine(newProjectDir.FullName, "Program.cs"), $"Console.Write(Environment.GetEnvironmentVariable(\"GoodEnvVar\"));");

        new DotnetCommand(TestContext, "build", "--configuration", "release", "/p:runtimeidentifier=linux-x64")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

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
        cni.ContainerRuntimeIdentifier = "linux-x64";
        cni.RuntimeIdentifierGraphPath = RuntimeGraphFilePath();

        Assert.IsTrue(cni.Execute());

        new BasicCommand(TestContext, "docker", "run", "--rm", $"{pcp.NewContainerImageName}:latest")
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Foo");
    }
}
