using GitCatalog.Generation;
using GitCatalog.Governance;
using GitCatalog.Import;
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
			"import-sqlserver" => RunImportSqlServer(args, repoRoot),
			_ => RunUnknownCommand(command)
		};
	}

	private static int RunImportSqlServer(string[] args, string repoRoot)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("Missing SQL Server connection string.");
			Console.Error.WriteLine("Usage: gitcatalog import-sqlserver <connectionString> [repoRoot]");
			return 1;
		}

		try
		{
			var importer = new SqlServerImporter();
			var result = importer.ImportAsync(args[1], repoRoot).GetAwaiter().GetResult();
			Console.WriteLine($"Imported tables: {result.Tables.Count}");
			Console.WriteLine($"Wrote YAML files: {result.FilesWritten.Count}");
			return 0;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"SQL import failed: {ex.Message}");
			return 1;
		}
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

		var warnings = GovernanceEngine.Lint(loadResult.Tables).ToList();
		PrintLines(warnings);

		var er = MermaidGenerator.GenerateEr(loadResult.Tables);
		var outputPath = Path.Combine(repoRoot, "docs", "generated");
		Directory.CreateDirectory(outputPath);
		var erPath = Path.Combine(outputPath, "er.mmd");
		File.WriteAllText(erPath, er);
		Console.WriteLine($"Generated ER diagram: {erPath}");

		var docs = MarkdownGenerator.GenerateCatalogDocs(loadResult.Tables, warnings);
		foreach (var doc in docs)
		{
			var path = Path.Combine(outputPath, doc.RelativePath);
			var directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrWhiteSpace(directory))
			{
				Directory.CreateDirectory(directory);
			}

			File.WriteAllText(path, doc.Content);
		}

		Console.WriteLine($"Generated Markdown docs: {docs.Count}");

		var siteOutputPath = Path.Combine(repoRoot, "docs", "site");
		Directory.CreateDirectory(siteOutputPath);
		var siteAssets = StaticSiteGenerator.GenerateSiteAssets(loadResult.Tables);
		foreach (var asset in siteAssets)
		{
			var path = Path.Combine(siteOutputPath, asset.RelativePath);
			var directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrWhiteSpace(directory))
			{
				Directory.CreateDirectory(directory);
			}

			File.WriteAllText(path, asset.Content);
		}

		Console.WriteLine($"Generated site assets: {siteAssets.Count}");

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
		Console.WriteLine("Usage: gitcatalog <validate|lint|generate-all|import-sqlserver> [args]");
		Console.WriteLine("import-sqlserver args: <connectionString> [repoRoot]");
	}
}
