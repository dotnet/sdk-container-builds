using System.Containers;
using System.Diagnostics;

namespace Test.System.Containers.Filesystem;

[TestClass]
public class EndToEnd
{
    private const string BaseImage = "dotnet/sdk";
    private const string BaseImageSource = "mcr.microsoft.com/";
    private const string BaseImageTag = "6.0";
    
    private const string NewImageName = "foo/bar";

    [TestMethod]
    public async Task ManuallyPackDotnetApplication()
    {
        ProcessStartInfo startRegistry = new("docker", "run --publish 5000:5000 --detach registry:2")
        {
            RedirectStandardOutput = true,
        };

        using Process? registryProcess = Process.Start(startRegistry);
        Assert.IsNotNull(registryProcess);
        string? registryContainterId = await registryProcess.StandardOutput.ReadLineAsync();
        Assert.IsNotNull(registryContainterId);
        await registryProcess.WaitForExitAsync();
        Assert.AreEqual(0, registryProcess.ExitCode);

        try
        {
            Process pullBase = Process.Start("docker", $"pull {BaseImageSource}{BaseImage}:{BaseImageTag}");
            Assert.IsNotNull(pullBase);
            await pullBase.WaitForExitAsync();
            Assert.AreEqual(0, pullBase.ExitCode);

            Process tag = Process.Start("docker", $"tag {BaseImageSource}{BaseImage}:{BaseImageTag} localhost:5000/{BaseImage}:{BaseImageTag}");
            Assert.IsNotNull(tag);
            await tag.WaitForExitAsync();
            Assert.AreEqual(0, tag.ExitCode);

            Process pushBase = Process.Start("docker", $"push localhost:5000/{BaseImage}:{BaseImageTag}");
            Assert.IsNotNull(pushBase);
            await pushBase.WaitForExitAsync();
            Assert.AreEqual(0, pushBase.ExitCode);

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

            Registry registry = new Registry(new Uri("http://localhost:5000"));

            Image x = await registry.GetImageManifest(BaseImage, BaseImageTag);

            Layer l = Layer.FromDirectory(Path.Join("MinimalTestApp", "bin", "Debug", "net6.0", "linux-x64", "publish"), "/app");

            x.AddLayer(l);

            x.SetEntrypoint("/app/MinimalTestApp");

            // Push the image back to the local registry

            await registry.Push(x, NewImageName);

            // pull it back locally

            // Run the image

            ProcessStartInfo runInfo = new("docker", $"run --tty localhost:5000/{NewImageName}:latest")
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
        finally
        {
            Process shutdownRegistry = Process.Start("docker", $"stop {registryContainterId}");
            Assert.IsNotNull(shutdownRegistry);
            await shutdownRegistry.WaitForExitAsync();
            Assert.AreEqual(0, shutdownRegistry.ExitCode);
        }
    }
}
