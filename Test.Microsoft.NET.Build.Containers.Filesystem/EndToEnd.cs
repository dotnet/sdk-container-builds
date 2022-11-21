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

        Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(DockerRegistryManager.LocalRegistry));

        Image x = await registry.GetImageManifest(DockerRegistryManager.BaseImage, DockerRegistryManager.Net6ImageTag, "linux-x64");

        Layer l = Layer.FromDirectory(publishDirectory, "/app");

        x.AddLayer(l);

        x.SetEntrypoint(new [] {"/app/MinimalTestApp" });

        // Push the image back to the local registry

        await registry.Push(x, NewImageName(), "latest", DockerRegistryManager.BaseImage, Console.WriteLine);

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

        Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(DockerRegistryManager.LocalRegistry));

        Image x = await registry.GetImageManifest(DockerRegistryManager.BaseImage, DockerRegistryManager.Net6ImageTag, "linux-x64");

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

    private static async Task<string> BuildLocalApp(string tfm = "net6.0", string rid = "linux-x64")
    {
        DirectoryInfo d = new DirectoryInfo("MinimalTestApp");
        if (d.Exists)
        {
            d.Delete(recursive: true);
        }

        ProcessStartInfo psi = new("dotnet", $"new console -f {tfm} -o MinimalTestApp")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        Process dotnetNew = Process.Start(psi);

        Assert.IsNotNull(dotnetNew);
        await dotnetNew.WaitForExitAsync();
        Assert.AreEqual(0, dotnetNew.ExitCode, await dotnetNew.StandardOutput.ReadToEndAsync() + Environment.NewLine + await dotnetNew.StandardError.ReadToEndAsync());

        ProcessStartInfo publishPSI = rid is null ? new("dotnet", $"publish -bl MinimalTestApp") : new("dotnet", $"publish -bl MinimalTestApp -r {rid}"); 
        publishPSI.RedirectStandardOutput = true;
        publishPSI.RedirectStandardError = true;
        Process publish = Process.Start(publishPSI);
        Assert.IsNotNull(publish);
        await publish.WaitForExitAsync();
        Assert.AreEqual(0, publish.ExitCode, await publish.StandardOutput.ReadToEndAsync() + Environment.NewLine + await publish.StandardError.ReadToEndAsync());

        string publishDirectory = Path.Join("MinimalTestApp", "bin", "Debug", tfm, rid, "publish");
        return publishDirectory;
    }

    [TestMethod]
    public async Task EndToEnd_NoAPI()
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "CreateNewImageTest"));
        DirectoryInfo privateNuGetAssets = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "ContainerNuGet"));

        if (newProjectDir.Exists)
        {
            newProjectDir.Delete(recursive: true);
        }

        if (privateNuGetAssets.Exists)
        {
            privateNuGetAssets.Delete(recursive: true);
        }

        newProjectDir.Create();
        privateNuGetAssets.Create();
        var repoGlobalJson = Path.Combine("..", "..", "..", "..", "global.json");
        File.Copy(repoGlobalJson, Path.Combine(newProjectDir.FullName, "global.json"));

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

        // do not pollute the primary/global NuGet package store with the private package(s)
        info.Environment["NUGET_PACKAGES"] = privateNuGetAssets.FullName;

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

        info.Arguments = $"nuget add source {nupkgPath.FullName} --name local-temp";

        // Set up temp folder as "nuget feed"
        Process dotnetNugetAddSource = Process.Start(info);
        Assert.IsNotNull(dotnetNugetAddSource);
        await dotnetNugetAddSource.WaitForExitAsync();
        Assert.AreEqual(0, dotnetNugetAddSource.ExitCode);

        // Add package to the project
        info.Arguments = $"add package Microsoft.NET.Build.Containers --prerelease -f net7.0";
        Process dotnetPackageAdd = Process.Start(info);
        Assert.IsNotNull(dotnetPackageAdd);
        await dotnetPackageAdd.WaitForExitAsync();
        Assert.AreEqual(0, dotnetPackageAdd.ExitCode, dotnetPackageAdd.StandardOutput.ReadToEnd());

        string imageName = NewImageName();
        string imageTag = "1.0";

        info.Arguments = $"publish /p:publishprofile=DefaultContainer /p:runtimeidentifier=linux-x64 /bl" +
                          $" /p:ContainerBaseImage={DockerRegistryManager.FullyQualifiedBaseImageDefault}" +
                          $" /p:ContainerRegistry={DockerRegistryManager.LocalRegistry}" +
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

        var containerName = "test-container-1";
        ProcessStartInfo runInfo = new("docker", $"run --rm --name {containerName} --publish 5017:80 --detach {DockerRegistryManager.LocalRegistry}/{imageName}:{imageTag}")
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

        ProcessStartInfo logsPsi = new("docker", $"logs {appContainerId}") {
            RedirectStandardOutput = true
        };

        Process logs = Process.Start(logsPsi);
        Assert.IsNotNull(logs);
        await logs.WaitForExitAsync();

        Assert.AreEqual(true, everSucceeded, logs.StandardOutput.ReadToEnd());


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
        privateNuGetAssets.Delete(true);
    }

    [DataRow("linux-x86", false, "/app")] // packaging framework-dependent because missing runtime packs for x86 linux.
    [DataRow("linux-x64", true, "/app")]
    [DataRow("linux-arm", false, "/app")] // packaging framework-dependent because emulating arm on x64 Docker host doesn't work
    [DataRow("linux-arm64", false, "/app")] // packaging framework-dependent because emulating arm64 on x64 Docker host doesn't work
    [DataRow("win-x64", true, "C:\\app")]
    [TestMethod]
    public async Task CanPackageForAllSupportedContainerRIDs(string rid, bool isRIDSpecific, string workingDir) {
        if (rid == "win-x64") {
            Assert.Inconclusive("Cannot run Windows containers on Linux hosts (or at the same time as Linux containers), so skipping for now");
            return;
        }
        string publishDirectory = await BuildLocalApp(tfm : "net7.0", rid : (isRIDSpecific ? rid : null));

        // Build the image
        Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(DockerRegistryManager.LocalRegistry));

        Image x = await registry.GetImageManifest(DockerRegistryManager.BaseImage, DockerRegistryManager.Net7ImageTag, rid);

        Layer l = Layer.FromDirectory(publishDirectory, "/app");

        x.AddLayer(l);
        x.WorkingDirectory = workingDir;

        var entryPoint = DecideEntrypoint(rid, isRIDSpecific, "MinimalTestApp", workingDir);
        x.SetEntrypoint(entryPoint);

        // Load the image into the local Docker daemon

        await LocalDocker.Load(x, NewImageName(), rid, DockerRegistryManager.BaseImage);

        // Run the image

        ProcessStartInfo runInfo = new("docker", $"run --rm --tty {NewImageName()}:{rid}") {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        Process run = Process.Start(runInfo);
        Assert.IsNotNull(run);
        await run.WaitForExitAsync();

        Assert.AreEqual(0, run.ExitCode, run.StandardOutput.ReadToEnd() + Environment.NewLine + run.StandardError.ReadToEnd());

        string[] DecideEntrypoint(string rid, bool isRIDSpecific, string appName, string workingDir) {
            var binary = rid.StartsWith("win") ? $"{appName}.exe" : appName;
            if (isRIDSpecific) {
                return new[] { $"{workingDir}/{binary}" };
            } else {
                return new[] { "dotnet", $"{workingDir}/{binary}.dll" };
            }
        }
    }
}