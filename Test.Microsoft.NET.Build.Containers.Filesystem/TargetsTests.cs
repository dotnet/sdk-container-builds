// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using static Microsoft.NET.Build.Containers.KnownStrings.Properties;
using FluentAssertions;
using Microsoft.Build.Execution;

namespace Test.Microsoft.NET.Build.Containers.Targets;

[TestClass]
public class TargetsTests
{

    [DataRow(true, "/app/foo.exe")]
    [DataRow(false, "dotnet", "/app/foo.dll")]
    [TestMethod]
    public void CanSetEntrypointArgsToUseAppHost(bool useAppHost, params string[] entrypointArgs)
    {
        var (project, _) = ProjectInitializer.InitProject(new()
        {
            [UseAppHost] = useAppHost.ToString()
        }, projectName: $"{nameof(CanSetEntrypointArgsToUseAppHost)}_{useAppHost}_{String.Join("_", entrypointArgs)}");
        Assert.IsTrue(project.Build(ComputeContainerConfig));
        var computedEntrypointArgs = project.GetItems(ContainerEntrypoint).Select(i => i.EvaluatedInclude).ToArray();
        foreach (var (First, Second) in entrypointArgs.Zip(computedEntrypointArgs))
        {
            Assert.AreEqual(First, Second);
        }
    }

    [DataRow("WebApplication44", "webapplication44", true)]
    [DataRow("friendly-suspicious-alligator", "friendly-suspicious-alligator", true)]
    [DataRow("*friendly-suspicious-alligator", "", false)]
    [DataRow("web/app2+7", "web/app2-7", true)]
    [DataRow("Microsoft.Apps.Demo.ContosoWeb", "microsoft-apps-demo-contosoweb", true)]
    [TestMethod]
    public void CanNormalizeInputContainerNames(string projectName, string expectedContainerImageName, bool shouldPass)
    {
        var (project, _) = ProjectInitializer.InitProject(new()
        {
            [AssemblyName] = projectName
        }, projectName: $"{nameof(CanNormalizeInputContainerNames)}_{projectName}_{expectedContainerImageName}_{shouldPass}");
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.AreEqual(shouldPass, instance.Build(new[] { ComputeContainerConfig }, null, null, out var outputs), "Build should have succeeded");
        Assert.AreEqual(expectedContainerImageName, instance.GetPropertyValue(ContainerImageName));
    }

    [DataRow("7.0.100", true)]
    [DataRow("8.0.100", true)]
    [DataRow("7.0.100-preview.7", true)]
    [DataRow("7.0.100-rc.1", true)]
    [DataRow("6.0.100", false)]
    [DataRow("7.0.100-preview.1", false)]
    [TestMethod]
    public void CanWarnOnInvalidSDKVersions(string sdkVersion, bool isAllowed)
    {
        var (project, _) = ProjectInitializer.InitProject(new()
        {
            ["NETCoreSdkVersion"] = sdkVersion,
            ["PublishProfile"] = "DefaultContainer"
        }, projectName: $"{nameof(CanWarnOnInvalidSDKVersions)}_{sdkVersion}_{isAllowed}");
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        var derivedIsAllowed = Boolean.Parse(project.GetProperty("_IsSDKContainerAllowedVersion").EvaluatedValue);
        // var buildResult = instance.Build(new[]{"_ContainerVerifySDKVersion"}, null, null, out var outputs);
        Assert.AreEqual(isAllowed, derivedIsAllowed, $"SDK version {(isAllowed ? "should" : "should not")} have been allowed ");
    }

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public void GetsConventionalLabelsByDefault(bool shouldEvaluateLabels)
    {
        var (project, _) = ProjectInitializer.InitProject(new()
        {
            [ContainerGenerateLabels] = shouldEvaluateLabels.ToString()
        }, projectName: $"{nameof(GetsConventionalLabelsByDefault)}_{shouldEvaluateLabels}");
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerConfig }, null, null, out var outputs).Should().BeTrue("Build should have succeeded");
        if (shouldEvaluateLabels)
        {
            instance.GetItems(ContainerLabel).Should().NotBeEmpty("Should have evaluated some labels by default");
        }
        else
        {
            instance.GetItems(ContainerLabel).Should().BeEmpty("Should not have evaluated any labels by default");
        }
    }

    private static bool LabelMatch(string label, string value, ProjectItemInstance item) => item.EvaluatedInclude == label && item.GetMetadata("Value") is { } v && v.EvaluatedValue == value;

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public void ShouldNotIncludeSourceControlLabelsUnlessUserOptsIn(bool includeSourceControl)
    {
        var commitHash = "abcdef";
        var repoUrl = "https://git.cosmere.com/shard/whimsy.git";

        var (project, _) = ProjectInitializer.InitProject(new()
        {
            ["PublishRepositoryUrl"] = includeSourceControl.ToString(),
            ["PrivateRepositoryUrl"] = repoUrl,
            ["SourceRevisionId"] = commitHash
        }, projectName: $"{nameof(ShouldNotIncludeSourceControlLabelsUnlessUserOptsIn)}_{includeSourceControl}");
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerConfig }, null, null, out var outputs).Should().BeTrue("Build should have succeeded");
        var labels = instance.GetItems(ContainerLabel);
        if (includeSourceControl)
        {
            labels.Should().NotBeEmpty("Should have evaluated some labels by default")
                .And.ContainSingle(label => LabelMatch("org.opencontainers.image.source", repoUrl, label))
                .And.ContainSingle(label => LabelMatch("org.opencontainers.image.revision", commitHash, label)); ;
        }
        else
        {
            labels.Should().NotBeEmpty("Should have evaluated some labels by default")
                .And.NotContain(label => LabelMatch("org.opencontainers.image.source", repoUrl, label))
                .And.NotContain(label => LabelMatch("org.opencontainers.image.revision", commitHash, label)); ;
        };
    }

}