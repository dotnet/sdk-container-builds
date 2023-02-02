using global::Microsoft.NET.Build.Containers;

namespace Test.Microsoft.NET.Build.Containers;

[TestClass]
public class DockerDaemonTests {


    [TestMethod]
    public async Task Can_detect_when_no_daemon_is_running() {
        // mimic no daemon running by setting the DOCKER_HOST to a nonexistent socket
        try {
            System.Environment.SetEnvironmentVariable("DOCKER_HOST", "tcp://123.123.123.123:12345");
            var available = await new LocalDocker().IsAvailable();
            Assert.IsFalse(available, "No daemon should be listening at that port");
        } finally {
            System.Environment.SetEnvironmentVariable("DOCKER_HOST", null);
        }
    }

    [TestMethod]
    public async Task Can_detect_when_daemon_is_running() {
        var available = await new LocalDocker().IsAvailable();
        Assert.IsFalse(available, "No daemon should be listening at that port");
    }
}