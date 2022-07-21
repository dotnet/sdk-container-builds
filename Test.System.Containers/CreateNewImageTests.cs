using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Containers.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

#nullable disable

namespace Test.System.Containers
{
    [TestClass]
    public class CreateNewImageTests
    {
        [TestMethod]
        public void BasicCall()
        {
            CreateNewImage task = new CreateNewImage();
            task.BaseImageName = "dotnet/runtime";
            task.BaseImageTag = "6.0";
            task.InputRegistryURL = "https://localhost:5000";
            task.OutputRegistryURL = "https://localhost:5000";

            ITaskItem[] files = new ITaskItem[1];
            files[0] = new TaskItem("foo.bar");

            task.Files = files;
            task.WorkingDirectory = "app/";
            task.NewImageName = "dotnet/newapp";
            task.Entrypoint = "dotnet newapp.dll";

            task.Execute();
        }

        [TestMethod]
        public async Task EndToEnd()
        {
            DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "CreateNewImageTest"));
            DirectoryInfo pathForLocalNugetSource = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "NuGetSource"));
            //string previousNugetPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES", );
            //Environment.SetEnvironmentVariable("NUGET_PACKAGES", pathForLocalNugetSource.FullName);

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
                // Build System.Containers.Tasks.csproj & wait.
                Assert.Fail();
            }

            ProcessStartInfo info = new ProcessStartInfo
            {
                WorkingDirectory = newProjectDir.FullName,
                FileName = "dotnet",
                Arguments = "new webapi -f net7.0"
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

            info.Arguments = $"nuget add source --name local-temp {pathForLocalNugetSource.FullName}";

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
            info.Arguments = $"add package System.Containers.Tasks --source local-temp -v 1.0.0";
            Process dotnetPackageAdd = Process.Start(info);
            Assert.IsNotNull(dotnetPackageAdd);
            await dotnetPackageAdd.WaitForExitAsync();
            Assert.AreEqual(0, dotnetPackageAdd.ExitCode);

            info.Arguments = "publish /p:publishprofile=defaultcontainer /p:runtimeidentifier=win-x64 /bl";
            // Build & publish the project
            Process publish = Process.Start(info);
            Assert.IsNotNull(publish);
            await publish.WaitForExitAsync();
            Assert.AreEqual(0, publish.ExitCode);

            Console.WriteLine(publish.StandardOutput.ReadToEndAsync());

            newProjectDir.Delete(true);
        }
    }
}
