namespace GoatLab.Server.Services;

/// <summary>
/// Minimal .env loader for local development. Walks up from the content root
/// looking for a .env file and sets every KEY=VALUE line as a process-level
/// environment variable unless already set. The Docker path sets env vars via
/// docker-compose directly, so this is only useful when running via
/// `dotnet run` from the host. No dependencies on DotNetEnv etc.
/// </summary>
public static class DotEnvLoader
{
    public static void Load(string startDir)
    {
        var path = FindEnvFile(startDir);
        if (path is null) return;

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();

            // Strip matching surrounding quotes — common in .env files.
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') ||
                 (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            // Don't overwrite existing env vars (host/container wins over .env).
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static string? FindEnvFile(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
