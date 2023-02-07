namespace Test.Microsoft.NET.Build.Containers.Filesystem;

[TestClass]
public static class TestInitialization {

    [AssemblyInitialize]
    public static void Initialize(TestContext ctx) {
        DockerRegistryManager.StartAndPopulateDockerRegistry(ctx);
        ProjectInitializer.LocateMSBuild(ctx);
        Directory.CreateDirectory(TestSettings.TestArtifactsDirectory);
    }

    [AssemblyCleanup]
    public static void Cleanup() {
        DockerRegistryManager.ShutdownDockerRegistry();
        ProjectInitializer.Cleanup();
        //clean up tests artifacts
        try
        {
            if (Directory.Exists(TestSettings.TestArtifactsDirectory))
            {
                Directory.Delete(TestSettings.TestArtifactsDirectory, true);
            }
        }
        catch { }
    }
}
