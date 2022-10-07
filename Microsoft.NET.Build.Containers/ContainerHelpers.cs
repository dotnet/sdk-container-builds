namespace Microsoft.NET.Build.Containers;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using static Patterns;

static class Patterns {
    
    private static readonly string alphaNumeric = @"[a-z0-9]+";
    private static readonly string separator = @"(?:[._]|__|[-]*)";
    private static readonly string nameComponent = expression(alphaNumeric, optional(repeated(separator, alphaNumeric)));
    private static readonly string domainNameComponent = @"(?:[a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9])";
    private static readonly string ipv6address = expression(
        literal("["),
        @"(?:[a-fA-F0-9:]+)",
        literal("]")
    );

    private static readonly string domainName = expression(
        domainNameComponent,
        optional(repeated(literal("."), domainNameComponent))
    );

    private static readonly string host = $"(?:{domainName}|{ipv6address})";

    private static readonly string domain = expression(
        host, 
        optional(literal(":"), "[0-9]+")
    );

    public static readonly Regex DomainRegexp = new (domain);

    private static readonly string tag = @"[\w][\w.-]{0,127}";

    public static readonly Regex TagRegexp = new(tag);

    private static readonly string anchoredTag = anchored(tag);

    public static readonly Regex anchoredTagRegexp = new(anchoredTag);

    // needed because the original golang used `[[:xdigit:]] which .Net doesn't support
    private static readonly string hexDigit = "[0-9A-Fa-f]";
    private static readonly string digestPat = $"[A-Za-z][A-Za-z0-9]*(?:[-_+.][A-Za-z][A-Za-z0-9]*)*[:]{hexDigit}{{32,}}";
    public static readonly Regex DigestRegexp = new(digestPat);
    private static readonly string anchoredDigest = anchored(digestPat);
    private static readonly Regex anchoredDigestRegexp = new(anchoredDigest);
    private static readonly string namePat = expression(
        optional(domain, literal("/")),
        nameComponent,
        optional(repeated(literal("/"), nameComponent))
    );

    public static readonly Regex NameRegexp = new(namePat);

    private static readonly string anchoredName = anchored(
        optional(capture(domain), literal("/")),
        capture(nameComponent, optional(repeated(literal("/"), nameComponent)))
    );

    public static readonly Regex anchoredNameRegexp = new(anchoredName);

    private static string referencePat = anchored(
        capture(namePat),
        optional(literal(":"), capture(tag)),
        optional(literal("@"), capture(digestPat))
    );

    public static Regex ReferenceRegexp = new(referencePat);

    private static readonly string identifier = @"([a-f0-9]{64})";
    public static readonly Regex IdentifierRegexp = new(identifier);

    private static readonly string shortIdentifier = @"([a-f0-9]{6,64})";
    public static readonly Regex ShortIdentifierRegexp = new(shortIdentifier);

    private static readonly string anchoredIdentifier = anchored(identifier); 
    private static readonly Regex anchoredIdentifierRegexp = new(anchoredIdentifier);

    private static readonly string anchoredShortIdentifier = anchored(shortIdentifier);
    private static readonly Regex anchoredShortIdentifierRegexp = new(anchoredShortIdentifier);

    private static string expression(params string[] segments) {
        var b = new StringBuilder();
        foreach (var s in segments) {
            b.Append(s);
        }
        return b.ToString();
    }
    private static string capture(params string[] segments) => $"({expression(segments)})";
    private static string anchored(params string[] segments) => $"^{expression(segments)}$";
    private static string literal(string s) => Regex.Escape(s);

    private static string repeated(params string[] segments) => $"{group(expression(segments))}+";
    private static string group(params string[] segments) => $"(?:{expression(segments)})";
    private static string optional(params string[] segments) => $"{group(expression(segments))}?";

}

record Label(string name, string value);

// Explicitly lowercase to ease parsing - the incoming values are
// lowercased by spec
public enum PortType
{
    tcp,
    udp
}

public record Port(int number, PortType type);

public static class ContainerHelpers
{

    public static string DefaultRegistry = "docker.io";

    /// <summary>
    /// Matches if the string is not lowercase or numeric, or ., _, or -.
    /// </summary>
    /// <remarks>Technically the period should be allowed as well, but due to inconsistent support between cloud providers we're removing it.</remarks>
    private static Regex imageNameCharacters = new Regex(@"[^a-z0-9_\-/]");

    /// <summary>
    /// Ensures the given registry is valid.
    /// </summary>
    /// <param name="registryName"></param>
    /// <returns></returns>
    public static bool IsValidRegistry(string registryName) => NameRegexp.IsMatch(registryName);

    /// <summary>
    /// Ensures the given image name is valid.
    /// Spec: https://github.com/opencontainers/distribution-spec/blob/4ab4752c3b86a926d7e5da84de64cbbdcc18d313/spec.md#pulling-manifests
    /// </summary>
    /// <param name="imageName"></param>
    /// <returns></returns>
    public static bool IsValidImageName(string imageName)
    {
        return anchoredNameRegexp.IsMatch(imageName);
    }

    /// <summary>
    /// Ensures the given tag is valid.
    /// Spec: https://github.com/opencontainers/distribution-spec/blob/4ab4752c3b86a926d7e5da84de64cbbdcc18d313/spec.md#pulling-manifests
    /// </summary>
    /// <param name="imageTag"></param>
    /// <returns></returns>
    public static bool IsValidImageTag(string imageTag)
    {
        return anchoredTagRegexp.IsMatch(imageTag);
    }

    /// <summary>
    /// Parse a fully qualified container name (e.g. https://mcr.microsoft.com/dotnet/runtime:6.0)
    /// Note: Tag not required.
    /// </summary>
    /// <param name="fullyQualifiedContainerName"></param>
    /// <param name="containerRegistry"></param>
    /// <param name="containerName"></param>
    /// <param name="containerTag"></param>
    /// <returns>True if the parse was successful. When false is returned, all out vars are set to empty strings.</returns>
    public static bool TryParseFullyQualifiedContainerName(string fullyQualifiedContainerName,
                                                            [NotNullWhen(true)] out string? containerRegistry,
                                                            [NotNullWhen(true)] out string? containerName,
                                                            [NotNullWhen(true)] out string? containerTag)
    {
        var referenceMatch = ReferenceRegexp.Match(fullyQualifiedContainerName);
        if(referenceMatch is not { Success: true }) {
            containerRegistry = null;
            containerName = null;
            containerTag = null;
            return false;
        }
        var nameMatch = anchoredNameRegexp.Match(referenceMatch.Groups[1].Value);
        if (nameMatch is { Success: true }) {
            if (nameMatch.Groups.Count == 3) {
                containerRegistry = nameMatch.Groups[1].Value;
                containerName = nameMatch.Groups[2].Value;
            } else {
                containerRegistry = DefaultRegistry;
                containerName = referenceMatch.Groups[1].Value;
            }
        } else {
            containerRegistry = null;
            containerName = null;
            containerTag = null;
            return false;
        }
        containerTag = referenceMatch.Groups[2].Value;
        return true;
    }

    /// <summary>
    /// Checks if a given container image name adheres to the image name spec. If not, and recoverable, then normalizes invalid characters.
    /// </summary>
    public static bool NormalizeImageName(string containerImageName,
                                         [NotNullWhen(false)] out string? normalizedImageName)
    {
        if (IsValidImageName(containerImageName))
        {
            normalizedImageName = null;
            return true;
        }
        else
        {
            if (!Char.IsLetterOrDigit(containerImageName, 0))
            {
                throw new ArgumentException("The first character of the image name must be a lowercase letter or a digit.");
            }
            var loweredImageName = containerImageName.ToLowerInvariant();
            normalizedImageName = imageNameCharacters.Replace(loweredImageName, "-");
            return false;
        }
    }

    [Flags]
    public enum ParsePortError
    {
        MissingPortNumber,
        InvalidPortNumber,
        InvalidPortType,
        UnknownPortFormat
    }

    public static bool TryParsePort(string? portNumber, string? portType, [NotNullWhen(true)] out Port? port, [NotNullWhen(false)] out ParsePortError? error)
    {
        var portNo = 0;
        error = null;
        if (String.IsNullOrEmpty(portNumber))
        {
            error = ParsePortError.MissingPortNumber;
        }
        else if (!int.TryParse(portNumber, out portNo))
        {
            error = ParsePortError.InvalidPortNumber;
        }

        if (!Enum.TryParse<PortType>(portType, out PortType t))
        {
            if (portType is not null)
            {
                error = (error ?? ParsePortError.InvalidPortType) | ParsePortError.InvalidPortType;
            }
            else
            {
                t = PortType.tcp;
            }
        }

        if (error is null)
        {
            port = new Port(portNo, t);
            return true;
        }
        else
        {
            port = null;
            return false;
        }

    }

    public static bool TryParsePort(string input, [NotNullWhen(true)] out Port? port, [NotNullWhen(false)] out ParsePortError? error)
    {
        var parts = input.Split('/');
        if (parts.Length == 2)
        {
            string portNumber = parts[0];
            string type = parts[1];
            return TryParsePort(portNumber, type, out port, out error);
        }
        else if (parts.Length == 1)
        {
            string portNum = parts[0];
            return TryParsePort(portNum, null, out port, out error);
        }
        else
        {
            error = ParsePortError.UnknownPortFormat;
            port = null;
            return false;
        }
    }
}
