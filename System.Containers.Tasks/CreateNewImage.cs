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

        public string PublishDirectory { get; set; }

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
                Log.LogError("No files to publish, aborting");
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

            // Turn the build output from items into an array of filepaths
            string[] filePaths = Files.Select((f) => f.ItemSpec).ToArray();

            // preserve the folder structure of items published
            string[] relativeFilePaths = filePaths.Select((x) => Path.GetRelativePath(PublishDirectory, x)).ToArray<string>();

            List<(string file, string relativePath)> filesWithPaths = new List<(string file, string relativePath)>();

            for (int i = 0; i < filePaths.Length; i++)
            {
                Log.LogMessage("File {0} has relative path of {1}", filePaths[i], relativeFilePaths[i]);
                filesWithPaths.Add((filePaths[i], relativeFilePaths[i]));
            }

            Layer newLayer = Layer.FromFiles(filesWithPaths.AsEnumerable());

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