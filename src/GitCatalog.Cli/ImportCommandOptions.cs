namespace GitCatalog.Cli;

public sealed record ImportCommandOptions(
    string ConnectionString,
    string RepoRoot,
    bool DryRun,
    int TimeoutSeconds,
    bool UsesInlineConnectionString,
    string? Error = null)
{
    public bool IsValid => string.IsNullOrWhiteSpace(Error);
}

public static class ImportCommandOptionsParser
{
    public static ImportCommandOptions Parse(string[] args, string defaultRepoRoot)
    {
        var dryRun = false;
        var timeoutSeconds = 120;
        string? connectionEnv = null;
        string? connectionFile = null;
        var positionals = new List<string>();

        for (var i = 1; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                positionals.Add(token);
                continue;
            }

            if (token.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                dryRun = true;
                continue;
            }

            if (token.Equals("--connection-env", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadOptionValue(args, ref i, out var value))
                {
                    return Error("Missing value for --connection-env");
                }

                connectionEnv = value;
                continue;
            }

            if (token.Equals("--connection-file", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadOptionValue(args, ref i, out var value))
                {
                    return Error("Missing value for --connection-file");
                }

                connectionFile = value;
                continue;
            }

            if (token.Equals("--timeout-seconds", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadOptionValue(args, ref i, out var value) || !int.TryParse(value, out timeoutSeconds) || timeoutSeconds <= 0)
                {
                    return Error("--timeout-seconds must be a positive integer");
                }

                continue;
            }

            return Error($"Unknown option: {token}");
        }

        var namedSources = 0;
        if (!string.IsNullOrWhiteSpace(connectionEnv))
        {
            namedSources++;
        }

        if (!string.IsNullOrWhiteSpace(connectionFile))
        {
            namedSources++;
        }

        if (namedSources > 1)
        {
            return Error("Use only one of --connection-env or --connection-file");
        }

        string? connectionString = null;
        var usesInline = false;

        if (namedSources == 1)
        {
            if (positionals.Count > 1)
            {
                return Error("When using --connection-env or --connection-file, provide at most one positional [repoRoot]");
            }

            if (positionals.Count == 1 && LooksLikeConnectionString(positionals[0]))
            {
                return Error("Connection source conflict: remove inline connection string when using --connection-env or --connection-file");
            }

            if (!string.IsNullOrWhiteSpace(connectionEnv))
            {
                connectionString = Environment.GetEnvironmentVariable(connectionEnv!);
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    return Error($"Environment variable '{connectionEnv}' is empty or missing");
                }
            }
            else
            {
                var path = Path.GetFullPath(connectionFile!);
                if (!File.Exists(path))
                {
                    return Error($"Connection file not found: {path}");
                }

                connectionString = File.ReadAllText(path).Trim();
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    return Error($"Connection file is empty: {path}");
                }
            }
        }
        else
        {
            if (positionals.Count < 1)
            {
                return Error("Missing SQL Server connection string");
            }

            connectionString = positionals[0];
            usesInline = true;
        }

        var repoRoot = defaultRepoRoot;
        if (namedSources == 1 && positionals.Count == 1)
        {
            repoRoot = Path.GetFullPath(positionals[0]);
        }

        if (namedSources == 0 && positionals.Count > 1)
        {
            repoRoot = Path.GetFullPath(positionals[1]);
        }

        return new ImportCommandOptions(connectionString!, repoRoot, dryRun, timeoutSeconds, usesInline);
    }

    private static bool TryReadOptionValue(string[] args, ref int index, out string value)
    {
        value = string.Empty;
        if (index + 1 >= args.Length)
        {
            return false;
        }

        if (args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            return false;
        }

        value = args[++index];
        return true;
    }

    private static bool LooksLikeConnectionString(string value)
        => value.Contains('=') && value.Contains(';');

    private static ImportCommandOptions Error(string message)
        => new(string.Empty, string.Empty, DryRun: false, TimeoutSeconds: 120, UsesInlineConnectionString: false, Error: message);
}
