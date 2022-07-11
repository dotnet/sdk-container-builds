using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Containers.Tasks;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
