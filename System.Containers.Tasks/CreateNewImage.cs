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
        /// The base registry to pull from.
        /// Ex: https://mcr.microsoft.com
        /// </summary>
        [Required]
        public string BaseRegistry { get; set; }

        /// <summary>
        /// The base image to pull.
        /// Ex: dotnet/runtime
        /// </summary>
        [Required]
        public string BaseImageName { get; set; }

        /// <summary>
        /// The base image tag.
        /// Ex: 6.0
        /// </summary>
        [Required]
        public string BaseImageTag { get; set; }

        /// <summary>
        /// The registry to push to.
        /// </summary>
        [Required]
        public string OutputRegistry { get; set; }

        /// <summary>
        /// The name of the image to push to the registry.
        /// </summary>
        [Required]
        public string ImageName { get; set; }

        /// <summary>
        /// The tag to associate with the image.
        /// </summary>
        public string ImageTag { get; set; }

        /// <summary>
        /// The directory for the build outputs to be published.
        /// Constructed from "$(MSBuildProjectDirectory)\$(PublishDir)"
        /// </summary>
        [Required]
        public string PublishDirectory { get; set; }

        /// <summary>
        /// The working directory for the container.
        /// </summary>
        [Required]
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// The entrypoint for the container.
        /// </summary>
        [Required]
        public string Entrypoint { get; set; }

        /// <summary>
        /// Arguments to pass alongside Entrypoint.
        /// </summary>
        public string EntrypointArgs { get; set; }


        public override bool Execute()
        {
            if (string.IsNullOrEmpty(PublishDirectory))
            {
                Log.LogError(string.Format("PublishDirectory is must have a value"));
                return !Log.HasLoggedErrors;
            }
            else if (!Directory.Exists(PublishDirectory))
            {
                Log.LogError(string.Format("PublishDirectory does not exist."));
                return !Log.HasLoggedErrors;
            }

            Registry reg;
            Image image;

            try
            {
                reg = new Registry(new Uri(BaseRegistry, UriKind.RelativeOrAbsolute));
                image = reg.GetImageManifest(BaseImageName, BaseImageTag).Result;
            }
            catch (Exception e)
            {
                if (BuildEngine != null)
                {
                    Log.LogError("Failed getting image manifest: {0}", e);
                }
                return !Log.HasLoggedErrors;
            }

            if (BuildEngine != null)
            {
                Log.LogMessage($"Loading from directory: {PublishDirectory}");
            }
            
            Layer newLayer = Layer.FromDirectory(PublishDirectory, WorkingDirectory);
            image.AddLayer(newLayer);
            image.SetEntrypoint(Entrypoint, EntrypointArgs?.Split(' ').ToArray());

            if (OutputRegistry.StartsWith("docker://"))
            {
                // To Do: LocalDocker.Load();
            }
            else
            {
                Registry outputReg = new Registry(new Uri(OutputRegistry));
                try
                {
                    outputReg.Push(image, ImageName, BaseImageName).Wait();
                }
                catch (Exception e)
                {
                    if (BuildEngine != null)
                    {
                        Log.LogError("Failed to push to the output registry: {0}", e);
                    }
                    return !Log.HasLoggedErrors;
                }
            }

            return !Log.HasLoggedErrors;
        }
    }
}