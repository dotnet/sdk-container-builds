namespace Microsoft.NET.Build.Containers;
public class BaseImageNotFoundException : Exception
{
    public BaseImageNotFoundException(string specifiedRuntimeIdentifier, string repositoryName, string reference, IEnumerable<string> supportedRuntimeIdentifiers)
            : base($"The RuntimeIdentifier '{specifiedRuntimeIdentifier}' is not supported by {repositoryName}:{reference}. The supported RuntimeIdentifiers are {String.Join(",", supportedRuntimeIdentifiers)}") {}
}