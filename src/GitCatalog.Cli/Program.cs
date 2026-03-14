using GitCatalog.Generation;
using GitCatalog.Governance;
using GitCatalog.Serialization;
using GitCatalog.Validation;

namespace GitCatalog.Cli;

public static class Program
{
	public static int Main(string[] args)
	{
		if (args.Length == 0)
		{
			PrintHelp();
			return 1;
		}

		var command = args[0].Trim().ToLowerInvariant();
		var repoRoot = args.Length > 1 ? Path.GetFullPath(args[1]) : Directory.GetCurrentDirectory();

		return command switch
		{
			"validate" => RunValidate(repoRoot),
			"lint" => RunLint(repoRoot),
			"generate-all" => RunGenerateAll(repoRoot),
			_ => RunUnknownCommand(command)
		};
	}

	private static int RunValidate(string repoRoot)
	{
		var loadResult = CatalogLoader.Load(repoRoot);
		PrintLines(loadResult.Diagnostics);

		var errors = CatalogValidator.Validate(loadResult.Tables).ToList();
		PrintLines(errors);

		return loadResult.Diagnostics.Count == 0 && errors.Count == 0 ? 0 : 1;
	}

	private static int RunLint(string repoRoot)
	{
		var loadResult = CatalogLoader.Load(repoRoot);
		PrintLines(loadResult.Diagnostics);
		if (loadResult.Diagnostics.Count > 0)
		{
			return 1;
		}

		var warnings = GovernanceEngine.Lint(loadResult.Tables).ToList();
		PrintLines(warnings);
		return 0;
	}

	private static int RunGenerateAll(string repoRoot)
	{
		var loadResult = CatalogLoader.Load(repoRoot);
		PrintLines(loadResult.Diagnostics);

		var errors = CatalogValidator.Validate(loadResult.Tables).ToList();
		PrintLines(errors);
		if (loadResult.Diagnostics.Count > 0 || errors.Count > 0)
		{
			return 1;
		}

		var er = MermaidGenerator.GenerateEr(loadResult.Tables);
		var outputPath = Path.Combine(repoRoot, "docs", "generated");
		Directory.CreateDirectory(outputPath);
		var erPath = Path.Combine(outputPath, "er.mmd");
		File.WriteAllText(erPath, er);
		Console.WriteLine($"Generated ER diagram: {erPath}");

		return 0;
	}

	private static int RunUnknownCommand(string command)
	{
		Console.Error.WriteLine($"Unknown command: {command}");
		PrintHelp();
		return 1;
	}

	private static void PrintLines(IEnumerable<string> lines)
	{
		foreach (var line in lines)
		{
			Console.WriteLine(line);
		}
	}

	private static void PrintHelp()
	{
		Console.WriteLine("GitCatalog CLI");
		Console.WriteLine("Usage: gitcatalog <validate|lint|generate-all> [repoRoot]");
	}
}
