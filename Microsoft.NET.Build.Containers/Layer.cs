using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Microsoft.NET.Build.Containers;

public record struct Layer
{
    public Descriptor Descriptor { get; private set; }

    public string BackingFile { get; private set; }

    public static Layer FromDescriptor(Descriptor descriptor)
    {
        return new()
        {
            BackingFile = ContentStore.PathForDescriptor(descriptor),
            Descriptor = descriptor
        };
    }

    public static Layer FromDirectory(string directory, string containerPath)
    {
        var fileList =
            new DirectoryInfo(directory)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Select(fsi =>
                    {
                        return (fsi.FullName, Path.GetRelativePath(directory, fsi.FullName));
                    });
        return FromFiles(containerPath, fileList);
    }

    /// <summary>
    /// Convert a windows rooted directory path to a unix rooted directory path.
    /// </summary>
    private static string NormalizeDirectoryPath(string directoryPath)
    {
        if (directoryPath.StartsWith('/')) {
            return directoryPath.TrimStart('/');
        } else if (Char.IsAsciiLetter(directoryPath[0]) && directoryPath.IndexOf(':') is var colonIndex && colonIndex > 0) {
            return directoryPath.Substring(colonIndex + 2).Replace("\\", "/"); // skip everything up to :\\ in the windows path and switch line endings
        } else {
            throw new ArgumentException("Invalid directory path", nameof(directoryPath));
        }
    }

    private static void WriteDirectory(TarWriter writer, DirectoryInfo containerDirectory) {
        var directoryPath = NormalizeDirectoryPath(containerDirectory.FullName);
        if (directoryPath == "") return;
        var entry = new GnuTarEntry(TarEntryType.Directory, directoryPath);
        entry.Mode = UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite
                    | UnixFileMode.GroupExecute | UnixFileMode.GroupRead
                    | UnixFileMode.OtherExecute | UnixFileMode.OtherRead;
        writer.WriteEntry(entry);
    }

    private static void WriteFile(TarWriter writer, FileInfo localFile, string destinationPath) {
        var entry = new GnuTarEntry(TarEntryType.RegularFile, destinationPath.TrimStart('/'));
        entry.Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite
                    | UnixFileMode.GroupRead
                    | UnixFileMode.OtherRead;
        if (localFile.Extension == ".exe" || localFile.Extension == "") {
            entry.Mode = entry.Mode | UnixFileMode.UserExecute;
        }
        entry.AccessTime = localFile.LastAccessTimeUtc;
        entry.ModificationTime = localFile.LastWriteTimeUtc;
        entry.ChangeTime = localFile.LastWriteTimeUtc;
        entry.DataStream = localFile.OpenRead();
        writer.WriteEntry(entry);
    }

    private static IEnumerable<DirectoryInfo> WalkDirectories(string root) {
        var currentDir = new DirectoryInfo(root);
        while (currentDir != null) {
            yield return currentDir;
            currentDir = currentDir.Parent;
        }
    }

    public static Layer FromFiles(string containerRoot, IEnumerable<(string localFullPath, string containerRelativePath)> fileList)
    {
        long fileSize;
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        byte[] uncompressedHash;

        string tempTarballPath = ContentStore.GetTempFile();
        var knownDirectories = new HashSet<string>();
        using (FileStream fs = File.Create(tempTarballPath))
        {
            using (HashDigestGZipStream gz = new(fs, leaveOpen: true))
            {
                using (TarWriter writer = new(gz, TarEntryFormat.Gnu, leaveOpen: true))
                {
                    foreach(var rootDirectory in WalkDirectories(containerRoot).Reverse())
                    {
                        WriteDirectory(writer, rootDirectory);
                        knownDirectories.Add(rootDirectory.FullName);
                    }
                    foreach (var item in fileList)
                    {
                        string destinationPath = Path.Join(containerRoot, item.containerRelativePath).Replace(Path.DirectorySeparatorChar, '/');
                        var directoriesInPath = WalkDirectories(new FileInfo(destinationPath).DirectoryName!).Reverse();
                        foreach (var directory in directoriesInPath)
                        {
                            if (!knownDirectories.Contains(directory.FullName))
                            {
                                WriteDirectory(writer, directory);
                                knownDirectories.Add(directory.FullName);
                            }
                        }

                        // Docker treats a COPY instruction that copies to a path like `/app` by
                        // including `app/` as a directory, with no leading slash. Emulate that here.
                        WriteFile(writer, new FileInfo(item.localFullPath), destinationPath);
                    }
                } // Dispose of the TarWriter before getting the hash so the final data get written to the tar stream

                uncompressedHash = gz.GetHash();
            }

            fileSize = fs.Length;

            fs.Position = 0;

            SHA256.HashData(fs, hash);
        }

        string contentHash = Convert.ToHexString(hash).ToLowerInvariant();
        string uncompressedContentHash = Convert.ToHexString(uncompressedHash).ToLowerInvariant();

        Descriptor descriptor = new()
        {
            MediaType = "application/vnd.docker.image.rootfs.diff.tar.gzip", // TODO: configurable? gzip always?
            Size = fileSize,
            Digest = $"sha256:{contentHash}",
            UncompressedDigest = $"sha256:{uncompressedContentHash}",
        };

        string storedContent = ContentStore.PathForDescriptor(descriptor);

        Directory.CreateDirectory(ContentStore.ContentRoot);

        File.Move(tempTarballPath, storedContent, overwrite: true);

        Layer l = new()
        {
            Descriptor = descriptor,
            BackingFile = storedContent,
        };

        return l;
    }

    private readonly static char[] PathSeparators = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

    /// <summary>
    /// A stream capable of computing the hash digest of raw uncompressed data while also compressing it.
    /// </summary>
    private sealed class HashDigestGZipStream : Stream
    {
        private readonly SHA256 hashAlgorithm;
        private readonly CryptoStream sha256Stream;
        private readonly Stream compressionStream;

        public HashDigestGZipStream(Stream writeStream, bool leaveOpen)
        {
            hashAlgorithm = SHA256.Create();
            sha256Stream = new CryptoStream(Stream.Null, hashAlgorithm, CryptoStreamMode.Write);
            compressionStream = new GZipStream(writeStream, CompressionMode.Compress, leaveOpen);
        }

        public override bool CanWrite => true;

        public override void Write(byte[] buffer, int offset, int count)
        {
            sha256Stream.Write(buffer, offset, count);
            compressionStream.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            sha256Stream.Write(buffer);
            compressionStream.Write(buffer);
        }

        public override void Flush()
        {
            sha256Stream.Flush();
            compressionStream.Flush();
        }

        internal byte[] GetHash()
        {
            sha256Stream.FlushFinalBlock();
            return hashAlgorithm.Hash!;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                // dispose hashAlgorithm after sha256Stream since sha256Stream references/uses it
                sha256Stream.Dispose();
                hashAlgorithm.Dispose();

                compressionStream.Dispose();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        // This class is never used with async writes, but if it ever is, implement these overrides
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => throw new NotImplementedException();
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override long Length => throw new NotImplementedException();
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
        public override void SetLength(long value) => throw new NotImplementedException();
    }
}