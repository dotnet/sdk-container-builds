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
        /// Base image for the container, constructed as: $(ContainerBaseImageName):$(ContainerBaseImageTag)
        /// unless overridden.
        /// </summary>
        [Required]
        public string BaseImage { get; set; }

        [Required]
        public string InputRegistryURL { get; set; }

        [Required]
        public ITaskItem[] Files { get; set; }

        [Required]
        public string OutputRegistryURL { get; set; }

        [Required]
        public string NewBaseImage { get; set; }

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
            Log.LogError("GetBaseImage not implemented yet!");

            return false;
        }
    }
}
