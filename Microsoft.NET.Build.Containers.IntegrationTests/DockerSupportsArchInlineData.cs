// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Text.Json;
using Microsoft.DotNet.CommandUtils;
using Xunit.Sdk;

namespace Microsoft.NET.Build.Containers.IntegrationTests;
public class DockerSupportsArchInlineData : DataAttribute
{
    // tiny optimization - since there are many instances of this attribute we should only get
    // the daemon status once
    private static Task<bool> IsDaemonAvailable = new LocalDocker(Console.WriteLine).IsAvailable();

    // also an optimization - this doesn't change over time so we can compute it once
    private static string[] LinuxPlatforms = GetSupportedLinuxPlatforms();

    // another optimization - daemons don't switch types easily or quickly, so this is as good as static
    private static Task<bool> IsWindowsDaemon =
        IsDaemonAvailable.ContinueWith(t => t.Result && GetIsWindowsDaemon());

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
        if (IsDaemonAvailable.GetAwaiter().GetResult())
        {
            if (LinuxPlatforms.Contains(quemu_Arch))
            {
                return true;
            }
            else
            {
                if (IsWindowsDaemon.GetAwaiter().GetResult() && _arch.StartsWith("windows"))
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

    private static string[] GetSupportedLinuxPlatforms()
    {
        var platformsLine = new BasicCommand(null, "docker", "buildx", "inspect", "default").Execute().StdOut!.Split(Environment.NewLine).First(x => x.StartsWith("Platforms:"));
        return platformsLine.Substring("Platforms: ".Length).Split(",", StringSplitOptions.TrimEntries);
    }

    private static bool GetIsWindowsDaemon()
    {
        // the config json has an OSType property that is either "linux" or "windows" -
        // we can't use this for linux arch detection because that isn't enough information.
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
