// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

public interface ILocalDaemon {
    public Task Load(Image image, ImageReference sourceReference, ImageReference destinationReference);
    public Task<bool> IsAvailable();
}