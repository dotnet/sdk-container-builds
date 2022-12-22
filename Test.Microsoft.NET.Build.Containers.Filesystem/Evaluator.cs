using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;

namespace Test.Microsoft.NET.Build.Containers;

public static class Evaluator {
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

    public static void LocateMSBuild(TestContext ctx)
    {
        var instances = MSBuildLocator.RegisterDefaults();
        var relativePath = Path.Combine("..", "packaging", "build", "Microsoft.NET.Build.Containers.targets");
        var targetsFile = CurrentFile.Relative(relativePath);
        var propsFile = Path.ChangeExtension(targetsFile, ".props");
        CombinedTargetsLocation = CombineFiles(propsFile, targetsFile);
    }

    public static void Cleanup()
    {
        if (CombinedTargetsLocation != null) File.Delete(CombinedTargetsLocation);
    }

    public static (Project, CapturingLogger) InitProject(Dictionary<string, string> bonusProps, bool captureLogs = false, [CallerMemberName]string projectName = "")
    {
        var props = new Dictionary<string, string>();
        // required parameters
        props["TargetFileName"] = "foo.dll";
        props["AssemblyName"] = "foo";
        props["_TargetFrameworkVersionWithoutV"] = "7.0";
        props["_NativeExecutableExtension"] = ".exe"; //TODO: windows/unix split here
        props["Version"] = "1.0.0"; // TODO: need to test non-compliant version strings here
        props["NetCoreSdkVersion"] = "7.0.100"; // TODO: float this to current SDK?
        // test setup parameters so that we can load the props/targets/tasks 
        props["ContainerCustomTasksAssembly"] = Path.GetFullPath(Path.Combine(".", "Microsoft.NET.Build.Containers.dll"));
        props["_IsTest"] = "true";
        var loggers = new List<ILogger>
        {
            new global::Microsoft.Build.Logging.BinaryLogger() {CollectProjectImports = global::Microsoft.Build.Logging.BinaryLogger.ProjectImportsCollectionMode.Embed, Verbosity = LoggerVerbosity.Diagnostic, Parameters = $"LogFile={projectName}.binlog" },
            new global::Microsoft.Build.Logging.ConsoleLogger(LoggerVerbosity.Detailed)
        };
        CapturingLogger logs = new CapturingLogger();
        loggers.Add(logs);

        var collection = new ProjectCollection(null, loggers, ToolsetDefinitionLocations.Default);
        foreach (var kvp in bonusProps)
        {
            props[kvp.Key] = kvp.Value;
        }
        return (collection.LoadProject(CombinedTargetsLocation, props, null), logs);
    }
}

public class CapturingLogger : ILogger
{
    public LoggerVerbosity Verbosity { get => LoggerVerbosity.Diagnostic; set { } }
    public string Parameters { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    private List<BuildMessageEventArgs> _messages = new();
    public IReadOnlyList<BuildMessageEventArgs> Messages {get  { return _messages; } }

    private List<BuildWarningEventArgs> _warnings = new();
    public IReadOnlyList<BuildWarningEventArgs> Warnings {get  { return _warnings; } }

    private List<BuildErrorEventArgs> _errors = new();
    public IReadOnlyList<BuildErrorEventArgs> Errors {get  { return _errors; } }

    public void Initialize(IEventSource eventSource)
    {
        eventSource.MessageRaised += (o, e) => _messages.Add(e);
        eventSource.WarningRaised += (o, e) => _warnings.Add(e);
        eventSource.ErrorRaised += (o, e) => _errors.Add(e);
    }


    public void Shutdown()
    {
    }
}