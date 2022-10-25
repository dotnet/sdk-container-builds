using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;

namespace Test.Microsoft.NET.Build.Containers.Targets;

[TestClass]
public class TargetsTests
{
    private static string CombinedTargetsLocation;

    private static string CombineFiles(string propsFile, string targetsFile)
    {
        var propsContent = File.ReadAllLines(propsFile);
        var targetsContent = File.ReadAllLines(targetsFile);
        var combinedContent = new List<string>();
        combinedContent.AddRange(propsContent[..^1]);
        combinedContent.AddRange(targetsContent[1..]);
        var tempTargetLocation = Path.Combine(Path.GetTempPath(), "Containers", "Microsoft.NET.Build.Containers.targets");
        Directory.CreateDirectory(Path.GetDirectoryName(tempTargetLocation));
        File.WriteAllLines(tempTargetLocation, combinedContent);
        return tempTargetLocation;
    }

    [ClassInitialize]
    public static void CombinePropsAndTargets(TestContext ctx)
    {
        var relativePath = Path.Combine("..", "packaging", "build", "Microsoft.NET.Build.Containers.targets");
        var targetsFile = CurrentFile.Relative(relativePath);
        var propsFile = Path.ChangeExtension(targetsFile, ".props");
        CombinedTargetsLocation = CombineFiles(propsFile, targetsFile);
    }

    [ClassCleanup]
    public static void Cleanup()
    {
        if (CombinedTargetsLocation != null) File.Delete(CombinedTargetsLocation);
    }

    private (Project, IDisposable) InitProject(Dictionary<string, string> bonusProps, string logFileName = "log")
    {
        var props = new Dictionary<string, string>();
        // required parameters
        props["TargetFileName"] = "foo.dll";
        props["AssemblyName"] = "foo";
        props["_TargetFrameworkVersionWithoutV"] = "7.0";
        props["_NativeExecutableExtension"] = ".exe"; //TODO: windows/unix split here
        props["Version"] = "1.0.0"; // TODO: need to test non-compliant version strings here
        props["NETCoreSdkVersion"] = "7.0.100"; // we manipulate this value during evaluation, so we need a good default.
                                                // tests that rely on checking this value can override it with bonusProps.

        // test setup parameters so that we can load the props/targets/tasks 
        props["CustomTasksAssembly"] = Path.GetFullPath(Path.Combine(".", "Microsoft.NET.Build.Containers.dll"));
        props["_IsTest"] = "true";

        var loggers = new List<ILogger>
        {
            // new global::Microsoft.Build.Logging.BinaryLogger() {CollectProjectImports = global::Microsoft.Build.Logging.BinaryLogger.ProjectImportsCollectionMode.Embed, Verbosity = LoggerVerbosity.Diagnostic, Parameters = $"LogFile={logFileName}.binlog" },
            new global::Microsoft.Build.Logging.ConsoleLogger(LoggerVerbosity.Detailed)
        };
        var collection = new ProjectCollection(null, loggers, ToolsetDefinitionLocations.Default);
        foreach (var kvp in bonusProps)
        {
            props[kvp.Key] = kvp.Value;
        }
        var p = collection.LoadProject(CombinedTargetsLocation, props, null);

        return (p, collection);
    }

    [DataRow(true, "/app/foo.exe")]
    [DataRow(false, "dotnet", "/app/foo.dll")]
    [TestMethod]
    public void CanSetEntrypointArgsToUseAppHost(bool useAppHost, params string[] entrypointArgs)
    {
        var (project, dispose) = InitProject(new()
        {
            ["UseAppHost"] = useAppHost.ToString()
        });
        using var _ = dispose;
        Assert.IsTrue(project.Build("ComputeContainerConfig"));
        var computedEntrypointArgs = project.GetItems("ContainerEntrypoint").Select(i => i.EvaluatedInclude).ToArray();
        foreach (var (First, Second) in entrypointArgs.Zip(computedEntrypointArgs))
        {
            Assert.AreEqual(First, Second);
        }
    }

    [DataRow("WebApplication44", "webapplication44", true)]
    [DataRow("friendly-suspicious-alligator", "friendly-suspicious-alligator", true)]
    [DataRow("*friendly-suspicious-alligator", "", false)]
    [DataRow("web/app2+7", "web/app2-7", true)]
    [DataRow("Microsoft.Apps.Demo.ContosoWeb", "microsoft-apps-demo-contosoweb", true)]
    [TestMethod]
    public void CanNormalizeInputContainerNames(string projectName, string expectedContainerImageName, bool shouldPass)
    {
        var (project, dispose) = InitProject(new()
        {
            ["AssemblyName"] = projectName
        });
        using var _ = dispose;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.AreEqual(shouldPass, instance.Build(new[]{"ComputeContainerConfig"}, null, null, out var outputs), "Build should have succeeded");
        Assert.AreEqual(expectedContainerImageName, instance.GetPropertyValue("ContainerImageName"));
    }

    [DataRow("7.0.100", true)]
    [DataRow("8.0.100", true)]
    [DataRow("7.0.100-preview.7", true)]
    [DataRow("7.0.100-rc.1", true)]
    [DataRow("6.0.100", false)]
    [DataRow("7.0.100-preview.1", false)]
    [TestMethod]
    public void CanWarnOnInvalidSDKVersions(string sdkVersion, bool isAllowed) {
        var (project, dispose) = InitProject(new()
        {
            ["NETCoreSdkVersion"] = sdkVersion,
            ["PublishProfile"] = "DefaultContainer"
        }, $"version-test-{sdkVersion}");
        using var _ = dispose;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        var derivedIsAllowed = Boolean.Parse(project.GetProperty("_IsSDKContainerAllowedVersion").EvaluatedValue);
        // var buildResult = instance.Build(new[]{"_ContainerVerifySDKVersion"}, null, null, out var outputs);
        Assert.AreEqual(isAllowed, derivedIsAllowed, $"SDK version {(isAllowed ? "should" : "should not")} have been allowed ");
    }
}