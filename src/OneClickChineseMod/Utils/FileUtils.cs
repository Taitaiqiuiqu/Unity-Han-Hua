using System.IO;

namespace OneClickChineseMod.Utils;

public static class FileUtils
{
    public static void SafeExtractZip(Stream zipStream, string targetPath)
    {
        var resolvedTarget = Path.GetFullPath(targetPath) + Path.DirectorySeparatorChar;

        using var archive = new System.IO.Compression.ZipArchive(zipStream);
        foreach (var entry in archive.Entries)
        {
            var fullPath = Path.GetFullPath(Path.Combine(targetPath, entry.FullName));

            // 路径穿越检查：entry.FullName 可能包含 ../../ 跳出目标目录。
            if (!fullPath.StartsWith(resolvedTarget, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Zip 条目路径非法（路径穿越）: {entry.FullName}");

            if (string.IsNullOrEmpty(entry.Name))
            {
                if (!Directory.Exists(fullPath))
                    Directory.CreateDirectory(fullPath);
                continue;
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using var entryStream = entry.Open();
            using var fileStream = File.Create(fullPath);
            entryStream.CopyTo(fileStream);
        }
    }

    public static void CopyDirectory(string source, string dest, bool recursive = true)
    {
        var dir = new DirectoryInfo(source);

        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {source}");

        var dirs = dir.GetDirectories();

        Directory.CreateDirectory(dest);

        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(dest, file.Name);
            file.CopyTo(targetFilePath, overwrite: true);
        }

        if (recursive)
        {
            foreach (var subDir in dirs)
            {
                var newDest = Path.Combine(dest, subDir.Name);
                CopyDirectory(subDir.FullName, newDest, recursive);
            }
        }
    }

    public static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    public static bool HasWritePermission(string path)
    {
        try
        {
            var testFile = Path.Combine(path, ".write_test_" + Guid.NewGuid().ToString("N"));
            using (File.Create(testFile)) { }
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string CalculateChecksum(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public static long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path))
            return 0;

        return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
            .Sum(f => new FileInfo(f).Length);
    }
}
