using System.Containers;
using System.Diagnostics;

namespace Test.System.Containers.Filesystem;

[TestClass]
public class EndToEnd
{
    private const string NewImageName = "dotnetcontainers/testimage";

    [TestMethod]
    public async Task ManuallyPackDotnetApplication()
    {
        // Create project

        DirectoryInfo d = new DirectoryInfo("MinimalTestApp");
        if (d.Exists)
        {
            d.Delete(recursive: true);
        }

        Process dotnetNew = Process.Start("dotnet", "new console -f net6.0 -o MinimalTestApp");
        Assert.IsNotNull(dotnetNew);
        await dotnetNew.WaitForExitAsync();
        Assert.AreEqual(0, dotnetNew.ExitCode);

        // Build project

        Process publish = Process.Start("dotnet", "publish -bl MinimalTestApp -r linux-x64");
        Assert.IsNotNull(publish);
        await publish.WaitForExitAsync();
        Assert.AreEqual(0, publish.ExitCode);

        // Build the image

        Registry registry = new Registry(new Uri("http://localhost:5010"));

        Image x = await registry.GetImageManifest(DockerRegistryManager.BaseImage, DockerRegistryManager.BaseImageTag);

        Layer l = Layer.FromDirectory(Path.Join("MinimalTestApp", "bin", "Debug", "net6.0", "linux-x64", "publish"), "/app");

        x.AddLayer(l);

        x.SetEntrypoint("/app/MinimalTestApp");

        // Push the image back to the local registry

        await registry.Push(x, NewImageName, DockerRegistryManager.BaseImage);

        // pull it back locally

        Process pull = Process.Start("docker", $"pull localhost:5010/{NewImageName}:latest");
        Assert.IsNotNull(pull);
        await pull.WaitForExitAsync();
        Assert.AreEqual(0, pull.ExitCode);

        // Run the image

        ProcessStartInfo runInfo = new("docker", $"run --tty localhost:5010/{NewImageName}:latest")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        Process? run = Process.Start(runInfo);
        Assert.IsNotNull(run);
        string? stdout = await run.StandardOutput.ReadToEndAsync();
        await run.WaitForExitAsync();

        Console.WriteLine("stdout: " + stdout);
        Console.WriteLine("stderr: " + await run.StandardError.ReadToEndAsync());

        Assert.AreEqual(0, run.ExitCode);

        Assert.IsTrue(stdout.Contains("Hello, World!"));
    }
}
