namespace System.Containers
{
    public static class ContainerHelpers
    {
        private static string[] knownImageSeparators = { "-", ".", "_" };

        /// <summary>
        /// Given some "fully qualified" image name (e.g. mcr.microsoft.com/dotnet/runtime), return
        /// a valid UriBuilder. This means appending 'https' if the URI is not absolute, otherwise UriBuilder will throw.
        /// </summary>
        /// <param name="containerBase"></param>
        /// <returns>A UriBuilder with the given containerBase, or, if containerBase is relative, https:// + containerBase</returns>
        public static UriBuilder ContainerImageToUriBuilder(string containerBase)
        {
            Uri uri = new Uri(containerBase, UriKind.RelativeOrAbsolute);

            if (uri.IsAbsoluteUri)
            {
                return new UriBuilder(uri);
            }
            else
            {
                // todo: allow customization?
                return new UriBuilder(containerBase.Contains("localhost") ? "http://" : "https://" + uri);
            }
        }

        public static bool IsValidImageName(string imageName)
        {
            for (int i = 0; i < knownImageSeparators.Length; i++)
            {
                if (imageName.StartsWith(imageName[i]) || imageName.EndsWith(knownImageSeparators[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsValidImageTag(string imageTag)
        {
            return imageTag.Length <= 128 && !imageTag.StartsWith('.') && !imageTag.StartsWith('-');
        }

        /// <summary>
        /// Parse a fully qualified container name (e.g. https://mcr.microsoft.com/dotnet/runtime:6.0)
        /// Note: Tag may be empty.
        /// </summary>
        /// <param name="fullyQualifiedContainerName"></param>
        /// <param name="containerRegistry"></param>
        /// <param name="containerName"></param>
        /// <param name="containerTag"></param>
        /// <returns></returns>
        public static void ParseFullyQualifiedContainerName(UriBuilder fullyQualifiedContainerName, out string containerRegistry, out string containerName, out string containerTag)
        {
            // The first segment is the '/', create a string out of everything after.
            string image = fullyQualifiedContainerName.Uri.Segments.Skip(1).Aggregate((str, next) => str + next);

            // If the image has a ':', there's a tag we need to parse.
            int indexOfColon = image.IndexOf(':');

            if (fullyQualifiedContainerName.Host.Contains("localhost"))
            {
                containerRegistry = "http://" + fullyQualifiedContainerName.Host;
            }
            else
            {
                containerRegistry = "https://" + fullyQualifiedContainerName.Host;
            }

            containerRegistry = (fullyQualifiedContainerName.Host.Contains("localhost") ? "http://" : "https://") + fullyQualifiedContainerName.Host;
            containerName = indexOfColon == -1 ? image : image.Substring(0, indexOfColon);
            containerTag = indexOfColon == -1 ? "" : image.Substring(indexOfColon + 1);
        }
    }
}