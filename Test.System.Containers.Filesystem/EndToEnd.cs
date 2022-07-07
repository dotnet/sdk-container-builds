using System.Containers;
using System.Diagnostics;

namespace Test.System.Containers.Filesystem;

[TestClass]
public class EndToEnd
{
    private const string BaseImage = "dotnet/sdk";
    private const string BaseImageSource = "mcr.microsoft.com/";
    private const string BaseImageTag = "6.0";

    [TestMethod]
    public async Task ManuallyPackDotnetApplication()
    {
        ProcessStartInfo startRegistry = new("docker", "run -p 5000:5000 -d registry:2")
        {
            RedirectStandardOutput = true,
        };

        using Process? registryProcess = Process.Start(startRegistry);
        Assert.IsNotNull(registryProcess);

        string? registryContainterId = await registryProcess.StandardOutput.ReadLineAsync();
        Assert.IsNotNull(registryContainterId);

        await registryProcess.WaitForExitAsync();
        Assert.AreEqual(0, registryProcess.ExitCode);

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

        try
        {
            Registry registry = new Registry(new Uri("http://localhost:5000"));

            Image x = await registry.GetImageManifest(BaseImage, BaseImageTag);

            Layer l = Layer.FromDirectory(@"S:\play\helloworld6\bin\Debug\net6.0\linux-x64\publish\", "/app");

            x.AddLayer(l);

            x.SetEntrypoint("/app/helloworld6");

            await registry.Push(x, "foo/bar");
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
