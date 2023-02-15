// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.NET.Build.Containers.UnitTests;

public class DockerDaemonAvailableTheoryAttribute : TheoryAttribute
{
    // tiny optimization - since there are many instances of this attribute we should only get
    // the daemon status once
    private static Task<bool> IsDaemonAvailable = new LocalDocker(Console.WriteLine).IsAvailable();
    public DockerDaemonAvailableTheoryAttribute()
    {
        if (!IsDaemonAvailable.GetAwaiter().GetResult())
        {
            base.Skip = "Skipping test because Docker is not available on this host.";
        }
    }
}

public class DockerDaemonAvailableFactAttribute : FactAttribute
{
    // tiny optimization - since there are many instances of this attribute we should only get
    // the daemon status once
    private static Task<bool> IsDaemonAvailable = new LocalDocker(Console.WriteLine).IsAvailable();
    public DockerDaemonAvailableFactAttribute()
    {
        if (!IsDaemonAvailable.GetAwaiter().GetResult())
        {
            base.Skip = "Skipping test because Docker is not available on this host.";
        }
    }
}
