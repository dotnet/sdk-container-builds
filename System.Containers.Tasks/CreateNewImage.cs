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
    internal class CreateNewImage : Microsoft.Build.Utilities.Task
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
        public ITaskItem[] Files { get; set; }

        /// <summary>
        /// $(ContainerWorkingDirectory)
        /// </summary>
        [Required]
        public string WorkingDirectory { get; set; }

        [Required]
        public string OutputRegistryURL { get; set; }

        [Required]
        public string NewImageName { get; set; }

        [Required]
        public string Entrypoint { get; set; }

        /// <summary>
        /// CreateNewImage needs to:
        /// 1. Pull a base image (needs parameters: URL, BaseImage, BaseImageTag)
        /// 2. Add output of build as a new layer (Needs parameters: ITaskItem[] files || directory to glob)
        /// 3. Push image back to some registry (needs parameters: OutputURL, NewName, EntryPoint)
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            // Registry reg = new Registry(new Uri(InputRegistryURL));

            // Image image = reg.GetImageManifest(BaseImageName, BaseImageTag);

            // Layer newLayer = Layer.FromDirectory(x,y); x: directory of outputs, y: WorkingDirectory

            string[] filePaths = Files.Select((f) => f.ItemSpec).ToArray();

            // Layer newLayer = Layer.FromFiles(filePaths, WorkingDirectory);

            // image.AddLayer(newLayer);

            // image.SetEntrypoint(Entrypoint);

            // registry.Push(image, NewImageName);

            return true;
        }
    }
}
