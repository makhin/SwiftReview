using System.Security.Cryptography;

namespace ORP.Importer;

/// <summary>The bytes, identity and hash of one read of a source file.</summary>
public sealed class SaaSourceFileSnapshot
{
    internal SaaSourceFileSnapshot(string filePath, string rootDirectory, string relativePath, byte[] bytes)
    {
        FilePath = filePath;
        RootDirectory = rootDirectory;
        RelativePath = relativePath;
        Bytes = bytes;
        using var sha256 = SHA256.Create();
        ContentHash = BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    public string FilePath { get; }
    public string RootDirectory { get; }
    public string RelativePath { get; }
    public string ContentHash { get; }
    internal byte[] Bytes { get; }
}

public sealed class SaaSourceFileReader
{
    public SaaSourceFileSnapshot Read(string path, string rootDirectory)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetFullPath(rootDirectory);
        var relativePath = GetRelativePath(fullPath, root);
        return new SaaSourceFileSnapshot(fullPath, root, relativePath, File.ReadAllBytes(fullPath));
    }

    public static string GetRelativePath(string path, string rootDirectory)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!fullPath.StartsWith(root, comparison))
            throw new ArgumentException("The file must be inside the SAA messages directory.", nameof(path));
        var relative = fullPath.Substring(root.Length).Replace('\\', '/');
        if (relative.Length > 450)
            throw new FormatException("FilePath is longer than 450 characters.");
        return relative;
    }
}
