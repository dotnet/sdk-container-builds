// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Test.Microsoft.NET.Build.Containers.Filesystem
{
    internal static class TestSettings
    {
        /// <summary>
        /// Gets temporary location for test artifacts.
        /// </summary>
        internal static string TestArtifactsDirectory { get; } = Path.Combine(Path.GetTempPath(), "ContainersTests", DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture));
    }
}
