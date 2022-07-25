using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        /// <summary>
        /// Constructed from "$(MSBuildProjectDirectory)\$(PublishDir)"
        /// </summary>
        [Required]
        public string PublishDirectory { get; set; }

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
            if (string.IsNullOrEmpty(PublishDirectory) || !Directory.Exists(PublishDirectory))
            {
                Log.LogError("PublishDirectory and Files are both invalid. One valid parameter MUST be given to the CreateNewImage task.");
                return false;
            }

            Registry reg = new Registry(new Uri(InputRegistryURL));

            Image image;
            try
            {
                image = reg.GetImageManifest(BaseImageName, BaseImageTag).Result;
            }
            catch (Exception ex)
            {
                Log.LogError("GetImageManifest Failed: {0}.\n{1}", ex.Message, ex.InnerException);
                return false;
            }

            Log.LogMessage($"Loading from directory: {PublishDirectory}");
            Layer newLayer = Layer.FromDirectory(PublishDirectory, WorkingDirectory);
            image.AddLayer(newLayer);

            image.SetEntrypoint(Entrypoint, EntrypointArgs?.Split(' ').ToArray());

            Registry outputReg = new Registry(new Uri(OutputRegistryURL));

            try
            {
                outputReg.Push(image, NewImageName, BaseImageName).Wait();
            }
            catch (Exception e)
            {
                Log.LogError("Failed to push to the output registry: {0}\n{1}", e.Message, e.InnerException);
                return false;
            }

            return true;
        }
    }
}