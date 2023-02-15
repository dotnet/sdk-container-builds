// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Text.Json;
using Microsoft.DotNet.CommandUtils;
using Xunit.Sdk;

namespace Microsoft.NET.Build.Containers.IntegrationTests;
public class DockerSupportsArchInlineData : DataAttribute
{
    private readonly string _arch;
    private readonly object[] _data;

    public DockerSupportsArchInlineData(string arch, params object[] data)
    {
        _arch = arch;
        _data = data;
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {

        if (DaemonSupportsArch(_arch))
        {
            var totalArray = new object[_data.Length + 1];
            totalArray[0] = _arch;
            Array.Copy(_data, 0, totalArray, 1, _data.Length);
            return new[] { totalArray };
        };
        return Array.Empty<object[]>();
    }

    private bool DaemonSupportsArch(string quemu_Arch)
    {
        if (new LocalDocker(m => { return; }).IsAvailable().GetAwaiter().GetResult())
        {
            var platformsLine = new BasicCommand(null, "docker", "buildx", "inspect", "default").Execute().StdOut!.Split(Environment.NewLine).First(x => x.StartsWith("Platforms:"));
            var platforms = platformsLine.Substring("Platforms: ".Length).Split(",", StringSplitOptions.TrimEntries);
            if (platforms.Contains(quemu_Arch))
            {
                return true;
            }
            else
            {
                if (IsWindowsDaemon() && _arch.StartsWith("windows"))
                {
                    return true;
                }
                base.Skip = $"Skipping test because Docker daemon does not support {quemu_Arch}.";
                return false;
            }
        }
        else
        {
            base.Skip = "Skipping test because Docker is not available on this host.";
            return false;
        }
    }

    private static bool IsWindowsDaemon()
    {
        var config = LocalDocker.GetConfig().GetAwaiter().GetResult();
        if (config.RootElement.TryGetProperty("OSType", out JsonElement osTypeProperty))
        {
            return osTypeProperty.GetString() == "windows";
        }
        else
        {
            return false;
        }
    }
}
