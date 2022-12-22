using System.Diagnostics;
using Microsoft.Build.Locator;

namespace Test.Microsoft.NET.Build.Containers.Filesystem;

public class DockerRegistryManager
{
    public const string BaseImage = "dotnet/runtime";
    public const string BaseImageSource = "mcr.microsoft.com/";
    public const string BaseImageTag = "7.0";
    public const string LocalRegistry = "localhost:5010";
    public const string FullyQualifiedBaseImageDefault = $"{BaseImageSource}{BaseImage}:{BaseImageTag}";
    private static string s_registryContainerId;

    private static void Exec(string command, string args) {
        var psi = new ProcessStartInfo(command, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        var proc = Process.Start(psi);
        Assert.IsNotNull(proc);
        proc.WaitForExit();
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        var message = $"StdOut:\n{stdout}\nStdErr:\n{stderr}";
        Assert.AreEqual(0, proc.ExitCode, message);
    }

    [AssemblyInitialize]
    public static void StartAndPopulateDockerRegistry(TestContext context)
    {
        context.WriteLine("Spawning local registry");
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

        Exec("docker", $"pull {BaseImageSource}{BaseImage}:{BaseImageTag}");
        Exec("docker", $"tag {BaseImageSource}{BaseImage}:{BaseImageTag} {LocalRegistry}/{BaseImage}:{BaseImageTag}");
        Exec("docker", $"push {LocalRegistry}/{BaseImage}:{BaseImageTag}");
    }

    public static void ShutdownDockerRegistry()
    {
        Assert.IsNotNull(s_registryContainerId);

        Process shutdownRegistry = Process.Start("docker", $"stop {s_registryContainerId}");
        Assert.IsNotNull(shutdownRegistry);
        shutdownRegistry.WaitForExit();
        Assert.AreEqual(0, shutdownRegistry.ExitCode);
    }
}