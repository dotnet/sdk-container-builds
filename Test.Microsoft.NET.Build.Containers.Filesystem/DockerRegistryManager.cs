// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.DotNet.CommandUtils;

namespace Test.Microsoft.NET.Build.Containers.Filesystem;

public class DockerRegistryManager
{
    public const string BaseImage = "dotnet/runtime";
    public const string BaseImageSource = "mcr.microsoft.com/";
    public const string Net6ImageTag = "6.0";
    public const string Net7ImageTag = "7.0";
    public const string LocalRegistry = "localhost:5010";
    public const string FullyQualifiedBaseImageDefault = $"{BaseImageSource}{BaseImage}:{Net6ImageTag}";
    private static string? s_registryContainerId;

    [AssemblyInitialize]
    public static void StartAndPopulateDockerRegistry(TestContext context)
    {
        context.WriteLine("Spawning local registry");
        // setup a local registry that uses basic auth and TLS (required for any kind of auth)
        var registryArgs = new[] {
            "run",
            "--rm",
            "--publish", "5010:5000",
            "--detach",
            "--name", "sdk-testregistry",
            "-e", "REGISTRY_AUTH=htpasswd",
            "-e", "REGISTRY_AUTH_HTPASSWD_REALM=\"Registry Realm\"",
            "-v", $"{CurrentFile.Relative("auth")}:/auth",
            "-e", "REGISTRY_AUTH_HTPASSWD_PATH=/auth/htpasswd",
            "-v", $"{CurrentFile.Relative("certs")}:/certs",
            "-e", "REGISTRY_HTTP_TLS_CERTIFICATE=/certs/domain.crt",
            "-e", "REGISTRY_HTTP_TLS_KEY=/certs/domain.key",
            "registry:2"
        };
        CommandResult processResult = new BasicCommand(context, "docker", registryArgs).Execute();
        processResult.Should().Pass().And.HaveStdOut();
        using var reader = new StringReader(processResult.StdOut!);
        s_registryContainerId = reader.ReadLine();

        new BasicCommand(context, "docker", "login", "-u", "testuser", "-p", "testpassword", LocalRegistry)
            .Execute()
            .Should().Pass();

        foreach (var tag in new[] { Net6ImageTag, Net7ImageTag })
        {
            new BasicCommand(context, "docker", "pull", $"{BaseImageSource}{BaseImage}:{tag}")
                .Execute()
                .Should().Pass();

            new BasicCommand(context, "docker", "tag", $"{BaseImageSource}{BaseImage}:{tag}", $"{LocalRegistry}/{BaseImage}:{tag}")
                .Execute()
                .Should().Pass();

            new BasicCommand(context, "docker", "push", $"{LocalRegistry}/{BaseImage}:{tag}")
                .Execute()
                .Should().Pass();
        }
        var localCert = new X509Certificate2(CurrentFile.Relative("certs/domain.crt"));
        ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
    }

    public static void ShutdownDockerRegistry()
    {
        Assert.IsNotNull(s_registryContainerId);

        // new BasicCommand(null, "docker", "stop", s_registryContainerId)
        //     .Execute()
        //     .Should().Pass();
    }
}
