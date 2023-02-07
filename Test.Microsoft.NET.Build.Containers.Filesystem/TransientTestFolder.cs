// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using static System.IO.Path;

namespace Test.Microsoft.NET.Build.Containers.Filesystem;

/// <summary>
/// Helper class to clean up after tests that touch the filesystem.
/// </summary>
internal sealed class TransientTestFolder : IDisposable
{
    public readonly string Path = Combine(TestSettings.TestArtifactsDirectory, GetRandomFileName());
    public readonly DirectoryInfo DirectoryInfo;

    public TransientTestFolder()
    {
        DirectoryInfo = Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        Directory.Delete(Path, recursive: true);
    }
}
