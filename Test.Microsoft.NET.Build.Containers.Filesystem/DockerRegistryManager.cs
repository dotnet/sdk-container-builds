using System.Diagnostics;

namespace Test.Microsoft.NET.Build.Containers.Filesystem;

[TestClass]
public class DockerRegistryManager
{
    public const string BaseImage = "dotnet/runtime";
    public const string BaseImageSource = "mcr.microsoft.com/";
    public const string BaseImageTag = "6.0";
    public const string LocalRegistry = "localhost:5010";
    public const string FullyQualifiedBaseImageDefault = $"https://{BaseImageSource}{BaseImage}:{BaseImageTag}";
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

        Exec("docker", $"pull {BaseImageSource}{BaseImage}:{BaseImageTag}");
        Exec("docker", $"tag {BaseImageSource}{BaseImage}:{BaseImageTag} {LocalRegistry}/{BaseImage}:{BaseImageTag}");
        Exec("docker", $"push {LocalRegistry}/{BaseImage}:{BaseImageTag}");
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