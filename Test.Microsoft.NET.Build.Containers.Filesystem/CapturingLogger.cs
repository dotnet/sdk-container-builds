using Microsoft.Build.Framework;

namespace Test.Microsoft.NET.Build.Containers;

public class CapturingLogger : ILogger
{
    public LoggerVerbosity Verbosity { get => LoggerVerbosity.Diagnostic; set { } }
    public string Parameters { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    private List<BuildMessageEventArgs> _messages = new();
    public IReadOnlyList<BuildMessageEventArgs> Messages {get  { return _messages; } }

    private List<BuildWarningEventArgs> _warnings = new();
    public IReadOnlyList<BuildWarningEventArgs> Warnings {get  { return _warnings; } }

    private List<BuildErrorEventArgs> _errors = new();
    public IReadOnlyList<BuildErrorEventArgs> Errors {get  { return _errors; } }

    public void Initialize(IEventSource eventSource)
    {
        eventSource.MessageRaised += (o, e) => _messages.Add(e);
        eventSource.WarningRaised += (o, e) => _warnings.Add(e);
        eventSource.ErrorRaised += (o, e) => _errors.Add(e);
    }


    public void Shutdown()
    {
    }
}