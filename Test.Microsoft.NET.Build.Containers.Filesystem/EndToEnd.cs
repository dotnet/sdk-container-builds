using Microsoft.NET.Build.Containers;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Test.Microsoft.NET.Build.Containers.Filesystem;
#nullable disable

[TestClass]
public class EndToEnd
{
    public static string NewImageName([CallerMemberName] string callerMemberName = "")
    {
        bool normalized = ContainerHelpers.NormalizeImageName(callerMemberName, out string normalizedName);

        if (!normalized)
        {
            return normalizedName;
        }

        return callerMemberName;
    }

    [TestMethod]
    public async Task ApiEndToEndWithRegistryPushAndPull()
    {
        string publishDirectory = await BuildLocalApp();

        // Build the image

        Registry registry = new Registry(new Uri($"http://{DockerRegistryManager.LocalRegistry}"));

        Image x = await registry.GetImageManifest(DockerRegistryManager.BaseImage, DockerRegistryManager.BaseImageTag);

        Layer l = Layer.FromDirectory(publishDirectory, "/app");

        x.AddLayer(l);

        x.SetEntrypoint(new [] {"/app/MinimalTestApp" });

        // Push the image back to the local registry

        await registry.Push(x, NewImageName(), "latest", DockerRegistryManager.BaseImage);

        // pull it back locally

        Process pull = Process.Start("docker", $"pull {DockerRegistryManager.LocalRegistry}/{NewImageName()}:latest");
        Assert.IsNotNull(pull);
        await pull.WaitForExitAsync();
        Assert.AreEqual(0, pull.ExitCode);

        // Run the image

        ProcessStartInfo runInfo = new("docker", $"run --rm --tty {DockerRegistryManager.LocalRegistry}/{NewImageName()}:latest");
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

        await LocalDocker.Load(x, NewImageName(), "latest", DockerRegistryManager.BaseImage);

        // Run the image

        ProcessStartInfo runInfo = new("docker", $"run --rm --tty {NewImageName()}:latest");
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

        ProcessStartInfo psi = new("dotnet", "new console -f net6.0 -o MinimalTestApp")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        Process dotnetNew = Process.Start(psi);

        Assert.IsNotNull(dotnetNew);
        await dotnetNew.WaitForExitAsync();
        Assert.AreEqual(0, dotnetNew.ExitCode, await dotnetNew.StandardOutput.ReadToEndAsync() + await dotnetNew.StandardError.ReadToEndAsync());

        // Build project

        Process publish = Process.Start("dotnet", "publish -bl MinimalTestApp -r linux-x64");
        Assert.IsNotNull(publish);
        await publish.WaitForExitAsync();
        Assert.AreEqual(0, publish.ExitCode);

        string publishDirectory = Path.Join("MinimalTestApp", "bin", "Debug", "net6.0", "linux-x64", "publish");
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

        string imageName = NewImageName();
        string imageTag = "1.0";

        info.Arguments = $"publish /p:publishprofile=DefaultContainer /p:runtimeidentifier=linux-x64 /bl" +
                          $" /p:ContainerBaseImage={DockerRegistryManager.FullyQualifiedBaseImageDefault}" +
                          $" /p:ContainerRegistry=http://{DockerRegistryManager.LocalRegistry}" +
                          $" /p:ContainerImageName={imageName}" +
                          $" /p:Version={imageTag}";

        // Build & publish the project
        Process publish = Process.Start(info);
        Assert.IsNotNull(publish);
        await publish.WaitForExitAsync();
        Assert.AreEqual(0, publish.ExitCode, publish.StandardOutput.ReadToEnd());

        Process pull = Process.Start("docker", $"pull {DockerRegistryManager.LocalRegistry}/{imageName}:{imageTag}");
        Assert.IsNotNull(pull);
        await pull.WaitForExitAsync();
        Assert.AreEqual(0, pull.ExitCode);

        ProcessStartInfo runInfo = new("docker", $"run --rm --publish 5017:80 --detach {DockerRegistryManager.LocalRegistry}/{imageName}:{imageTag}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        Process run = Process.Start(runInfo);
        Assert.IsNotNull(run);
        await run.WaitForExitAsync();
        Assert.AreEqual(0, run.ExitCode);

        string appContainerId = (await run.StandardOutput.ReadToEndAsync()).Trim();

        bool everSucceeded = false;

        HttpClient client = new();

        // Give the server a moment to catch up, but no more than necessary.
        for (int retry = 0; retry < 10; retry++)
        {
            try
            {
                var response = await client.GetAsync("http://localhost:5017/weatherforecast");

                if (response.IsSuccessStatusCode)
                {
                    everSucceeded = true;
                    break;
                }
            }
            catch { }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Assert.AreEqual(true, everSucceeded);

        ProcessStartInfo stopPsi = new("docker", $"stop {appContainerId}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        Process stop = Process.Start(stopPsi);
        Assert.IsNotNull(stop);
        await stop.WaitForExitAsync();
        Assert.AreEqual(0, stop.ExitCode, stop.StandardOutput.ReadToEnd() + stop.StandardError.ReadToEnd());

        newProjectDir.Delete(true);
        pathForLocalNugetSource.Delete(true);
    }
}