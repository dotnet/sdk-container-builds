using System.Runtime.CompilerServices;

namespace Test.Microsoft.NET.Build.Containers;

public static class CurrentFile
{
    public static string Path([CallerFilePath] string file = "") => file;

    public static string Relative(string relative, [CallerFilePath] string file = "") {
        return global::System.IO.Path.Combine(global::System.IO.Path.GetDirectoryName(file), relative);
    }
}