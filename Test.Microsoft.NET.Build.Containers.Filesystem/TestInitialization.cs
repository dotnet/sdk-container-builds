namespace Test.Microsoft.NET.Build.Containers.Filesystem;

[TestClass]
public static class TestInitialization {

    [AssemblyInitialize]
    public static void Initialize(TestContext ctx) {
        DockerRegistryManager.StartAndPopulateDockerRegistry(ctx);
        Evaluator.LocateMSBuild(ctx);
    }

    [AssemblyCleanup]
    public static void Cleanup() {
        DockerRegistryManager.ShutdownDockerRegistry();
        Evaluator.Cleanup();
    }
}