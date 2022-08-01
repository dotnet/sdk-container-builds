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
    public class ParseContainerProperties : Microsoft.Build.Utilities.Task
    {

        private string[] knownImageSeparators = { "-", ".", "_" };

        /// <summary>
        /// The full base image name. mcr.microsoft.com/dotnet/runtime:6.0, for example.
        /// </summary>
        [Required]
        public string ContainerBaseImage { get; set; }

        [Required]
        public string ContainerImageName { get; set; }

        [Required]
        public string ContainerImageTag { get; set; }

        [Output]
        public string ParsedContainerRegistry { get; private set; }

        [Output]
        public string ParsedContainerImage { get; private set; }

        [Output]
        public string ParsedContainerTag { get; private set; }

        [Output]
        public string NewImageName { get; private set; }

        [Output]
        public string NewImageTag { get; private set; }

        public override bool Execute()
        {
            for (int i = 0; i < knownImageSeparators.Length; i++)
            {
                if (ContainerImageName.StartsWith(knownImageSeparators[i]) || ContainerImageName.EndsWith(knownImageSeparators[i]))
                {
                    Log.LogError("Container images cannot start or end with separators. Found: {0} in {1}", knownImageSeparators[i], ContainerImageName);
                    return false;
                }
            }

            if (ContainerImageTag.StartsWith('.') || ContainerImageTag.StartsWith('-'))
            {
                Log.LogError("Container tags cannot start with periods or dashes. Container tag was: {0}", ContainerImageTag);
                return false;
            }

            if (ContainerImageTag.Length > 128)
            {
                Log.LogError("The max length of a tag is 128 characters. Container tag's length was: {0}", ContainerImageTag.Length);
                return false;
            }

            // To do: What if user inputs URI that starts with https?
            UriBuilder uri;
            
            try
            {
                uri = new UriBuilder(ContainerBaseImage);
            }
            catch (Exception e)
            {
                Log.LogError("Failed to parse the given ContainerBaseImage: {0}", e.Message);
                return false;
            }

            // The first segment is the '/', create a string out of everything after.
            string image = uri.Uri.Segments.Skip(1).Aggregate((str, next) => str + next);

            // If the image has a ':', there's a tag we need to parse.
            int indexOfColon = image.IndexOf(':');

            if (uri.Host.Contains("localhost"))
            {
                ParsedContainerRegistry = "http://" + uri.Host;
            }
            else
            {
                ParsedContainerRegistry = "https://" + uri.Host;
            }

            ParsedContainerImage = indexOfColon == -1 ? image : image.Substring(0, indexOfColon);
            ParsedContainerTag = indexOfColon == -1 ? "" : image.Substring(indexOfColon + 1);
            NewImageName = ContainerImageName.ToLower().Replace(' ', '-');
            NewImageTag = ContainerImageTag.Replace(' ', '-');

            if (BuildEngine != null)
            {
                Log.LogMessage("Parsed the following properties. Note: Spaces are replaced with dashes.");
                Log.LogMessage("Host: {0}", ParsedContainerRegistry);
                Log.LogMessage("Image: {0}", ParsedContainerImage);
                Log.LogMessage("Tag: {0}", ParsedContainerTag);
                Log.LogMessage("Image Name: {0}", NewImageName);
                Log.LogMessage("Image Tag: {0}", NewImageTag);
            }

            return true;
        }
    }
}