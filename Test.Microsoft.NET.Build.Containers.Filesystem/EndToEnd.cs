using Microsoft.NET.Build.Containers;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace Test.Microsoft.NET.Build.Containers.Filesystem;
#nullable disable

[TestClass]
public class EndToEnd
{
    private const string NewImageName = "dotnetcontainers/testimage";

    [TestMethod]
    public async Task ApiEndToEndWithRegistryPushAndPull()
    {
        string publishDirectory = await BuildLocalApp();

        // Build the image

        Registry localRegistry = new Registry(new Uri($"http://{DockerRegistryManager.LocalRegistry}"));
        Registry acr = new Registry(new Uri($"https://ghcr.io"));

        Image x = await acr.GetImageManifest(DockerRegistryManager.BaseImage, DockerRegistryManager.BaseImageTag);

        Layer l = Layer.FromDirectory(publishDirectory, "/app");

        x.AddLayer(l);

        x.SetEntrypoint(new [] {"/app/MinimalTestApp" });

        // Push the image back to the local registry

        await localRegistry.Push(x, NewImageName, "latest", DockerRegistryManager.BaseImage);

        // pull it back locally

        Process pull = Process.Start("docker", $"pull {DockerRegistryManager.LocalRegistry}/{NewImageName}:latest");
        Assert.IsNotNull(pull);
        await pull.WaitForExitAsync();
        Assert.AreEqual(0, pull.ExitCode);

        // Run the image

        ProcessStartInfo runInfo = new("docker", $"run --rm --tty {DockerRegistryManager.LocalRegistry}/{NewImageName}:latest");
        Process run = Process.Start(runInfo);
        Assert.IsNotNull(run);
        await run.WaitForExitAsync();

        Assert.AreEqual(0, run.ExitCode);
    }

    [TestMethod]
    public async Task ApiEndToEndWithLocalLoad()
    {
        string publishDirectory = await BuildLocalApp();

        // Build the image

        Registry registry = new Registry(new Uri($"http://{DockerRegistryManager.LocalRegistry}"));

        Image x = await registry.GetImageManifest(DockerRegistryManager.BaseImage, DockerRegistryManager.BaseImageTag);

        Layer l = Layer.FromDirectory(publishDirectory, "/app");

        x.AddLayer(l);

        x.SetEntrypoint(new [] { "/app/MinimalTestApp" });

        // Load the image into the local Docker daemon

        await LocalDocker.Load(x, NewImageName, "latest", DockerRegistryManager.BaseImage);

        // Run the image

        ProcessStartInfo runInfo = new("docker", $"run --rm --tty {NewImageName}:latest");
        Process run = Process.Start(runInfo);
        Assert.IsNotNull(run);
        await run.WaitForExitAsync();

        Assert.AreEqual(0, run.ExitCode);
    }

    private static async Task<string> BuildLocalApp()
    {
        DirectoryInfo d = new DirectoryInfo("MinimalTestApp");
        if (d.Exists)
        {
            d.Delete(recursive: true);
        }

        ProcessStartInfo psi = new("dotnet", "new console -f net7.0 -o MinimalTestApp")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        Process dotnetNew = Process.Start(psi);

        Assert.IsNotNull(dotnetNew);
        await dotnetNew.WaitForExitAsync();
        Assert.AreEqual(0, dotnetNew.ExitCode, await dotnetNew.StandardOutput.ReadToEndAsync() + await dotnetNew.StandardError.ReadToEndAsync());

        // Build project
        var info = new ProcessStartInfo("dotnet", "publish -c Release -o MinimalTestApp")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = d.FullName
        };
        Process publish = Process.Start(info);
        Assert.IsNotNull(publish);
        await publish.WaitForExitAsync();
        Assert.AreEqual(0, publish.ExitCode, publish.StandardOutput.ReadToEnd());

        string publishDirectory = Path.Join("MinimalTestApp", "bin", "Debug", "net7.0", "linux-x64", "publish");
        return publishDirectory;
    }

    [TestMethod]
    public async Task EndToEnd_NoAPI()
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "CreateNewImageTest"));
        DirectoryInfo pathForLocalNugetSource = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "NuGetSource"));

        if (newProjectDir.Exists)
        {
            newProjectDir.Delete(recursive: true);
        }

        if (pathForLocalNugetSource.Exists)
        {
            pathForLocalNugetSource.Delete(recursive: true);
        }

        newProjectDir.Create();
        pathForLocalNugetSource.Create();

        // 🤢
        DirectoryInfo nupkgPath = new DirectoryInfo(Assembly.GetAssembly(this.GetType()).Location).Parent.Parent.Parent.Parent;
        nupkgPath = nupkgPath.GetDirectories("package")[0];
        FileInfo[] nupkgs = nupkgPath.GetFiles("*.nupkg");
        if (nupkgs == null || nupkgs.Length == 0)
        {
            // Build Microsoft.NET.Build.Containers.csproj & wait.
            // for now, fail.
            Assert.Fail("No nupkg found in expected package folder. You may need to rerun the build");
        }

        ProcessStartInfo info = new ProcessStartInfo
        {
            WorkingDirectory = newProjectDir.FullName,
            FileName = "dotnet",
            Arguments = "new webapi -f net7.0",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        // Create the project to pack
        Process dotnetNew = Process.Start(info);
        Assert.IsNotNull(dotnetNew);
        await dotnetNew.WaitForExitAsync();
        Assert.AreEqual(0, dotnetNew.ExitCode);

        // Give it a unique nugetconfig
        info.Arguments = "new nugetconfig";
        Process dotnetNewNugetConfig = Process.Start(info);
        Assert.IsNotNull(dotnetNewNugetConfig);
        await dotnetNewNugetConfig.WaitForExitAsync();
        Assert.AreEqual(0, dotnetNewNugetConfig.ExitCode);

        info.Arguments = $"nuget add source {pathForLocalNugetSource.FullName} --name local-temp";

        // Set up temp folder as "nuget feed"
        Process dotnetNugetAddSource = Process.Start(info);
        Assert.IsNotNull(dotnetNugetAddSource);
        await dotnetNugetAddSource.WaitForExitAsync();
        Assert.AreEqual(0, dotnetNugetAddSource.ExitCode);

        for (int i = 0; i < nupkgs.Length; i++)
        {
            // Push local nupkg to "nuget feed"
            info.Arguments = $"nuget push {nupkgs[i].FullName} --source local-temp";
            Process dotnetNugetPush = Process.Start(info);
            Assert.IsNotNull(dotnetNugetPush);
            await dotnetNugetPush.WaitForExitAsync();
            Assert.AreEqual(0, dotnetNugetPush.ExitCode);
        }

        // Add package to the project
        info.Arguments = $"add package Microsoft.NET.Build.Containers --prerelease -f net7.0";
        Process dotnetPackageAdd = Process.Start(info);
        Assert.IsNotNull(dotnetPackageAdd);
        await dotnetPackageAdd.WaitForExitAsync();
        Assert.AreEqual(0, dotnetPackageAdd.ExitCode);

        info.Arguments = $"publish /p:publishprofile=defaultcontainer /p:runtimeidentifier=linux-x64 /bl" +
                          $" /p:ContainerBaseImage={DockerRegistryManager.FullyQualifiedBaseImageDefault}" +
                          $" /p:ContainerRegistry=http://{DockerRegistryManager.LocalRegistry}" +
                          $" /p:ContainerImageName={NewImageName}" +
                          $" /p:Version=1.0";

        // Build & publish the project
        Process publish = Process.Start(info);
        Assert.IsNotNull(publish);
        await publish.WaitForExitAsync();
        Assert.AreEqual(0, publish.ExitCode, publish.StandardOutput.ReadToEnd());

        Process pull = Process.Start("docker", $"pull {DockerRegistryManager.LocalRegistry}/{NewImageName}:latest");
        Assert.IsNotNull(pull);
        await pull.WaitForExitAsync();
        Assert.AreEqual(0, pull.ExitCode);

        ProcessStartInfo runInfo = new("docker", $"run --rm --tty {DockerRegistryManager.LocalRegistry}/{NewImageName}:latest")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        Process run = Process.Start(runInfo);
        Assert.IsNotNull(run);
        string stdout = await run.StandardOutput.ReadToEndAsync();
        await run.WaitForExitAsync();

        Console.WriteLine("stdout: " + stdout);
        Console.WriteLine("stderr: " + await run.StandardError.ReadToEndAsync());

        Assert.AreEqual(0, run.ExitCode);
        newProjectDir.Delete(true);
        pathForLocalNugetSource.Delete(true);
    }
}