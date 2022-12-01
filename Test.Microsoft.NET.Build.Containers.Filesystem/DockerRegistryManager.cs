using System.Diagnostics;
using Microsoft.Build.Locator;

namespace Test.Microsoft.NET.Build.Containers.Filesystem;

[TestClass]
public class DockerRegistryManager
{
    public const string BaseImage = "dotnet/runtime";
    public const string BaseImageSource = "mcr.microsoft.com/";
    public const string Net6ImageTag = "6.0";
    public const string Net7ImageTag = "7.0";
    public const string LocalRegistry = "localhost:5010";
    public const string FullyQualifiedBaseImageDefault = $"{BaseImageSource}{BaseImage}:{Net6ImageTag}";
    private static string s_registryContainerId;

    private static void Exec(string command, string args) {
        var startInfo = new ProcessStartInfo(command, args){
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        Process cmd = Process.Start(startInfo);
        Assert.IsNotNull(cmd);
        cmd.WaitForExit();
        Assert.AreEqual(0, cmd.ExitCode, cmd.StandardOutput.ReadToEnd());
    }

    public static void LocateMSBuild()
    {
        var instances = MSBuildLocator.QueryVisualStudioInstances(new() { DiscoveryTypes = DiscoveryType.DotNetSdk, WorkingDirectory = Environment.CurrentDirectory });
        MSBuildLocator.RegisterInstance(instances.First());
    }

    [AssemblyInitialize]
    public static void StartAndPopulateDockerRegistry(TestContext context)
    {
        Console.WriteLine(nameof(StartAndPopulateDockerRegistry));

        ProcessStartInfo startRegistry = new("docker", "run --rm --publish 5010:5000 --detach registry:2")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using Process registryProcess = Process.Start(startRegistry);
        Assert.IsNotNull(registryProcess);
        string registryContainerId = registryProcess.StandardOutput.ReadLine();
        // debugging purposes
        string everythingElse = registryProcess.StandardOutput.ReadToEnd();
        string errStream = registryProcess.StandardError.ReadToEnd();
        Assert.IsNotNull(registryContainerId);
        registryProcess.WaitForExit();
        Assert.AreEqual(0, registryProcess.ExitCode, $"Could not start Docker registry. Are you running one for manual testing?{Environment.NewLine}{errStream}");

        s_registryContainerId = registryContainerId;

        foreach (var tag in new[] { Net6ImageTag, Net7ImageTag })
        {
            Exec("docker", $"pull {BaseImageSource}{BaseImage}:{tag}");
            Exec("docker", $"tag {BaseImageSource}{BaseImage}:{tag} {LocalRegistry}/{BaseImage}:{tag}");
            Exec("docker", $"push {LocalRegistry}/{BaseImage}:{tag}");
        }
        LocateMSBuild();
    }

    [AssemblyCleanup]
    public static void ShutdownDockerRegistry()
    {
        Assert.IsNotNull(s_registryContainerId);

        Process shutdownRegistry = Process.Start("docker", $"stop {s_registryContainerId}");
        Assert.IsNotNull(shutdownRegistry);
        shutdownRegistry.WaitForExit();
        Assert.AreEqual(0, shutdownRegistry.ExitCode);
    }
}