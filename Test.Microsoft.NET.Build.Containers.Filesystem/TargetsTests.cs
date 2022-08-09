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
    public static void LocateMSBuild(TestContext ctx)
    {
        var instances = MSBuildLocator.QueryVisualStudioInstances(new() { DiscoveryTypes = DiscoveryType.DotNetSdk, WorkingDirectory = Environment.CurrentDirectory });
        MSBuildLocator.RegisterInstance(instances.First());
        var relativePath = Path.Combine("..", "Microsoft.NET.Build.Containers", "build", "Microsoft.NET.Build.Containers.targets");
        var targetsFile = CurrentFile.Relative(relativePath);
        var propsFile = Path.ChangeExtension(targetsFile, ".props");
        CombinedTargetsLocation = CombineFiles(propsFile, targetsFile);
    }

    [ClassCleanup]
    public static void Cleanup()
    {
        if (CombinedTargetsLocation != null) File.Delete(CombinedTargetsLocation);
    }

    private Project InitProject(Dictionary<string, string> bonusProps)
    {
        var props = new Dictionary<string, string>();
        // required parameters
        props["TargetFileName"] = "foo.dll";
        props["AssemblyName"] = "foo";
        props["_TargetFrameworkVersionWithoutV"] = "7.0";
        props["_NativeExecutableExtension"] = ".exe"; //TODO: windows/unix split here
        props["Version"] = "1.0.0"; // TODO: need to test non-compliant version strings here

        // test setup parameters so that we can load the props/targets/tasks 
        props["CustomTasksAssembly"] = Path.GetFullPath(Path.Combine(".", "Microsoft.NET.Build.Containers.dll"));
        props["_IsTest"] = "true";

        var loggers = new List<ILogger>
        {
            // new Microsoft.Build.Logging.BinaryLogger() {CollectProjectImports = Microsoft.Build.Logging.BinaryLogger.ProjectImportsCollectionMode.Embed, Verbosity = LoggerVerbosity.Diagnostic, Parameters = "LogFile=blah.binlog" },
            // new global::Microsoft.Build.Logging.ConsoleLogger(LoggerVerbosity.Detailed)
        };
        var collection = new ProjectCollection(null, loggers, ToolsetDefinitionLocations.Default);
        foreach (var kvp in bonusProps)
        {
            props[kvp.Key] = kvp.Value;
        }
        return collection.LoadProject(CombinedTargetsLocation, props, null);
    }

    [DataRow(true, "/app/foo.exe")]
    [DataRow(false, "dotnet", "/app/foo.dll")]
    [TestMethod]
    public void CanSetEntrypointArgsToUseAppHost(bool useAppHost, params string[] entrypointArgs)
    {
        var project = InitProject(new()
        {
            ["UseAppHost"] = useAppHost.ToString()
        });
        Assert.IsTrue(project.Build("ComputeContainerConfig"));
        {
            var computedEntrypointArgs = project.GetItems("ContainerEntrypoint").Select(i => i.EvaluatedInclude).ToArray();
            foreach (var (First, Second) in entrypointArgs.Zip(computedEntrypointArgs))
            {
                Assert.AreEqual(First, Second);
            }
        }
    }
}