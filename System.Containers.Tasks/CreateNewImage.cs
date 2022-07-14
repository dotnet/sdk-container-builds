using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable disable

namespace System.Containers.Tasks
{
    public class CreateNewImage : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// Base image name.
        /// </summary>
        [Required]
        public string BaseImageName { get; set; }

        [Required]
        public string BaseImageTag { get; set; }

        [Required]
        public string InputRegistryURL { get; set; }

        [Required]
        public string OutputRegistryURL { get; set; }

        [Required]
        public ITaskItem[] Files { get; set; }

        /// <summary>
        /// $(ContainerWorkingDirectory)
        /// </summary>
        [Required]
        public string WorkingDirectory { get; set; }

        [Required]
        public string NewImageName { get; set; }

        [Required]
        public string Entrypoint { get; set; }

        /// <summary>
        /// Arguments to pass alongside Entrypoint.
        /// </summary>
        public string EntrypointArgs { get; set; }

        /// <summary>
        /// CreateNewImage needs to:
        /// 1. Pull a base image (needs parameters: URL, BaseImage, BaseImageTag)
        /// 2. Add output of build as a new layer
        /// 3. Push image back to some registry (needs parameters: OutputURL, NewName, EntryPoint)
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            if (Files.Length == 0)
            {
                Console.WriteLine("Files is empty, aborting.");
                return false;
            }

            Registry reg = new Registry(new Uri(InputRegistryURL));

            Image image;
            try
            {
                image = reg.GetImageManifest(BaseImageName, BaseImageTag).Result;
            }
            catch
            {
                Console.WriteLine("GetImageManifest failed");
                return false;
            }

            // Turn the build output (as items) into an array of filepaths
            string[] filePaths = Files.Select((f) => f.ItemSpec).ToArray();

            Layer newLayer = Layer.FromDirectory(Path.GetDirectoryName(filePaths[0]), WorkingDirectory);

            image.AddLayer(newLayer);
            image.SetEntrypoint(Entrypoint, EntrypointArgs?.Split(' ').ToArray());

            Registry outputReg = new Registry(new Uri(OutputRegistryURL));

            try
            {
                outputReg.Push(image, NewImageName).Wait();
            }
            catch
            {
                Console.WriteLine("Registry.Push failed");
                return false;
            }

            return true;
        }
    }
}
