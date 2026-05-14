namespace DatabazyApiStarter;

public static class LoadEnv
{
    public static bool LoadFromDefaultLocations(string fileName = ".env")
    {
        var currentDirectory = Directory.GetCurrentDirectory();

        while (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            var fullPath = Path.Combine(currentDirectory, fileName);

            if (LoadFile(fullPath))
            {
                Console.WriteLine($"Loaded environment variables from: {fullPath}");
                return true;
            }

            var parent = Directory.GetParent(currentDirectory);
            if (parent is null)
            {
                break;
            }

            currentDirectory = parent.FullName;
        }

        return false;
    }

    public static bool LoadFile(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split('=', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
        }

        return true;
    }
}
