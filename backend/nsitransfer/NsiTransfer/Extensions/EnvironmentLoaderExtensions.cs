using System.Collections.Generic;
using System.IO;

namespace NsiTransfer.Extensions;

public static class EnvironmentLoader
{
    public static void Load()
    {
        foreach (var dir in WalkUpDirectories(Directory.GetCurrentDirectory()))
        {
            var envPath = Path.Combine(dir, ".env");
            if (!File.Exists(envPath))
                continue;

            LoadFile(envPath);
            Console.WriteLine($"[env] Загружены переменные окружения из: {envPath}");
            break;
        }
    }

    private static void LoadFile(string path)
    {
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#"))
                continue;

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"').Trim('\'');

            if (string.IsNullOrEmpty(key))
                continue;

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static IEnumerable<string> WalkUpDirectories(string startDirectory)
    {
        var dir = Path.GetFullPath(startDirectory);
        while (!string.IsNullOrEmpty(dir))
        {
            yield return dir;
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent == dir || string.IsNullOrEmpty(parent))
                yield break;
            dir = parent;
        }
    }
}
