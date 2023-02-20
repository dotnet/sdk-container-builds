﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Microsoft.NET.Build.Containers.Resources {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Strings {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Strings() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Microsoft.NET.Build.Containers.Resources.Strings", typeof(Strings).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Value for unit test {0}.
        /// </summary>
        internal static string _Test {
            get {
                return ResourceManager.GetString("_Test", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Request to Amazon Elastic Container Registry failed prematurely. This is often caused when the target repository does not exist in the registry..
        /// </summary>
        internal static string AmazonRegistryFailed {
            get {
                return ResourceManager.GetString("AmazonRegistryFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to upload blob to {0}; received {1} with detail {2}..
        /// </summary>
        internal static string BlobUploadFailed {
            get {
                return ResourceManager.GetString("BlobUploadFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Could not deserialize token from JSON..
        /// </summary>
        internal static string CouldntDeserializeJsonToken {
            get {
                return ResourceManager.GetString("CouldntDeserializeJsonToken", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to get docker info({0})\n{1}\n{2}..
        /// </summary>
        internal static string DockerInfoFailed {
            get {
                return ResourceManager.GetString("DockerInfoFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed creating docker process..
        /// </summary>
        internal static string DockerProcessCreationFailed {
            get {
                return ResourceManager.GetString("DockerProcessCreationFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Don&apos;t know how to pull images from local daemons at the moment..
        /// </summary>
        internal static string DontKnowHowToPullImages {
            get {
                return ResourceManager.GetString("DontKnowHowToPullImages", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed retrieving credentials for &quot;{0}&quot;: {1}..
        /// </summary>
        internal static string FailedRetrievingCredentials {
            get {
                return ResourceManager.GetString("FailedRetrievingCredentials", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to load image to local Docker daemon. stdout: {0}..
        /// </summary>
        internal static string ImageLoadFailed {
            get {
                return ResourceManager.GetString("ImageLoadFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The first character of the image name must be a lowercase letter or a digit..
        /// </summary>
        internal static string InvalidImageName {
            get {
                return ResourceManager.GetString("InvalidImageName", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Token response had neither token nor access_token..
        /// </summary>
        internal static string InvalidTokenResponse {
            get {
                return ResourceManager.GetString("InvalidTokenResponse", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Expected base image to have a config node..
        /// </summary>
        internal static string MissingBaseImageConfigNode {
            get {
                return ResourceManager.GetString("MissingBaseImageConfigNode", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Tried to get layer information but there is no layer node?.
        /// </summary>
        internal static string MissingLayerNode {
            get {
                return ResourceManager.GetString("MissingLayerNode", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Need a good error for &apos;couldn&apos;t download a thing because no link to registry&apos;..
        /// </summary>
        internal static string MissingLinkToRegistry {
            get {
                return ResourceManager.GetString("MissingLinkToRegistry", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No RequestUri specified..
        /// </summary>
        internal static string NoRequestUriSpecified {
            get {
                return ResourceManager.GetString("NoRequestUriSpecified", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Registry push failed..
        /// </summary>
        internal static string RegistryPushFailed {
            get {
                return ResourceManager.GetString("RegistryPushFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Too many retries, stopping..
        /// </summary>
        internal static string TooManyRetries {
            get {
                return ResourceManager.GetString("TooManyRetries", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unknown local container daemon type &apos;{0}&apos;. Valid local container daemon types are {1}..
        /// </summary>
        internal static string UnknownDaemonType {
            get {
                return ResourceManager.GetString("UnknownDaemonType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The manifest for {0}:{1} from registry {2} was an unknown type: {3}. Please raise an issue at https://github.com/dotnet/sdk-container-builds/issues with this message..
        /// </summary>
        internal static string UnknownMediaType {
            get {
                return ResourceManager.GetString("UnknownMediaType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unrecognized mediaType &apos;{0}&apos;..
        /// </summary>
        internal static string UnrecognizedMediaType {
            get {
                return ResourceManager.GetString("UnrecognizedMediaType", resourceCulture);
            }
        }
    }
}
