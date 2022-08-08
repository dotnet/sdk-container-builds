using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;

namespace Test.Microsoft.NET.Build.Containers.Targets;

[TestClass]
public class TargetsTests
{
    [ClassInitialize]
    public static void LocateMSBuild(TestContext ctx)
    {
        var instances = MSBuildLocator.QueryVisualStudioInstances(new() { DiscoveryTypes = DiscoveryType.DotNetSdk, WorkingDirectory = Environment.CurrentDirectory });
        MSBuildLocator.RegisterInstance(instances.First());
    }

    private Project InitProject(Dictionary<string, string> bonusProps) {
        var targetsFile = Path.Combine("..", "..", "..", "..", "Microsoft.NET.Build.Containers.Tasks", "build", "Microsoft.NET.Build.Containers.Tasks.targets");
        var props = new Dictionary<string, string>();
        // required parameters
        props["TargetFileName"] = "foo.dll";
        props["AssemblyName"] = "foo";
        props["_TargetFrameworkVersionWithoutV"] = "7.0";
        props["_NativeExecutableExtension"] = ".exe"; //TODO: windows/unix split here
        props["Version"] = "1.0.0"; // TODO: need to test non-compliant version strings here

        // test setup parameters so that we can load the props/targets/tasks 
        props["CustomTasksAssembly"] = Path.GetFullPath(Path.Combine(".", "Microsoft.NET.Build.Containers.Tasks.dll"));
        props["_IsTest"] = "true";

        var loggers = new List<ILogger> {
            // new Microsoft.Build.Logging.BinaryLogger() {CollectProjectImports = Microsoft.Build.Logging.BinaryLogger.ProjectImportsCollectionMode.Embed, Verbosity = LoggerVerbosity.Diagnostic, Parameters = "LogFile=blah.binlog" },
            // new Microsoft.Build.Logging.ConsoleLogger(LoggerVerbosity.Detailed)
        };
        var collection = new ProjectCollection(null, loggers, ToolsetDefinitionLocations.Default);
        foreach (var kvp in bonusProps) {
            props[kvp.Key] = kvp.Value;
        }
        return collection.LoadProject(targetsFile, props, null);
    }

    [DataRow(true, "/app/foo.exe")]
    [DataRow(false, "dotnet", "/app/foo.dll")]
    [TestMethod]
    public void CanSetEntrypointArgsToUseAppHost(bool useAppHost, params string[] entrypointArgs)
    {
        var project = InitProject(new() {
            ["UseAppHost"] = useAppHost.ToString()
        });
        if (project.Build("ComputeContainerConfig"))
        {
            var computedEntrypointArgs = project.GetItems("ContainerEntrypoint").Select(i => i.EvaluatedInclude).ToArray();
            foreach (var (First, Second) in entrypointArgs.Zip(computedEntrypointArgs)) {
                Assert.AreEqual(First, Second);
            }
        }
    }
}