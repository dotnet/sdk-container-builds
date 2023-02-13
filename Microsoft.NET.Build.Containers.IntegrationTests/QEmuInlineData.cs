// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.DotNet.CommandUtils;
using Microsoft.NET.Build.Containers;
using Xunit.Sdk;

public class QemuInlineDataAttribute : DataAttribute
{
    private readonly string _quemu_Arch;
    private readonly object[] _data;

    public QemuInlineDataAttribute(string quemu_arch, params object[] data)
    {
        _quemu_Arch = quemu_arch;
        _data = data;
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod) {

        if(DaemonSupportsArch(_quemu_Arch)) {
            var totalArray = new object[_data.Length + 1];
            totalArray[0] = _quemu_Arch;
            Array.Copy(_data, 0, totalArray, 1, _data.Length);
            return new [] { totalArray };
        };
        return Array.Empty<object[]>();
    }

    private bool DaemonSupportsArch(string quemu_Arch) {
        if (new LocalDocker(m => { return; }).IsAvailable().GetAwaiter().GetResult()) {
            var platformsLine = new BasicCommand(null, "docker", "buildx", "inspect", "default").Execute().StdOut!.Split(Environment.NewLine).First(x => x.StartsWith("Platforms:"));
            var platforms = platformsLine.Substring("Platforms: ".Length).Split(",", StringSplitOptions.TrimEntries);
            if (platforms.Contains(quemu_Arch)) {
                return true;
            } else {
                base.Skip = $"Skipping test because Docker daemon does not support {quemu_Arch}.";
                return false;
            }
        } else {
            base.Skip = "Skipping test because Docker is not available on this host.";
            return false;
        }
    }
}
