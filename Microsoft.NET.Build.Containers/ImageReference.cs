// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

public readonly record struct ImageReference(Registry? Registry, string Repository, string Tag) {
    public override string ToString()
    {
        if (Registry is {} reg) {
            return $"{reg.BaseUri.GetComponents(UriComponents.HostAndPort, UriFormat.Unescaped)}/{Repository}:{Tag}";
        } else {
            return RepositoryAndTag;
        }
    }

    public readonly string RepositoryAndTag => $"{Repository}:{Tag}";
}
