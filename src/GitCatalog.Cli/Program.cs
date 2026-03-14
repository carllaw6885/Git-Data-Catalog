using GitCatalog.Generation;
using GitCatalog.Governance;
using GitCatalog.Import;
using GitCatalog.Serialization;
using GitCatalog.Validation;
using System.Text.RegularExpressions;

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
		var repoRoot = Directory.GetCurrentDirectory();

		if (command is "validate" or "lint" or "generate-all")
		{
			repoRoot = args.Length > 1 ? Path.GetFullPath(args[1]) : repoRoot;
		}

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
		var parsed = ImportCommandOptionsParser.Parse(args, repoRoot);
		if (!parsed.IsValid)
		{
			Console.Error.WriteLine(parsed.Error);
			Console.Error.WriteLine("Usage: gitcatalog import-sqlserver [--dry-run] [--timeout-seconds <n>] (<connectionString> | --connection-env <name> | --connection-file <path>) [repoRoot]");
			return 1;
		}

		try
		{
			var importer = new SqlServerImporter();
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(parsed.TimeoutSeconds));
			var result = importer.ImportAsync(parsed.ConnectionString, parsed.RepoRoot, new ImportOptions(parsed.DryRun), cts.Token).GetAwaiter().GetResult();
			PrintLines(result.Warnings);
			foreach (var change in result.Changes)
			{
				Console.WriteLine($"{change.Kind}: {change.Summary}");
				foreach (var detail in change.DriftDetails)
				{
					Console.WriteLine($"  - {detail}");
				}
			}
			Console.WriteLine($"Imported tables: {result.Tables.Count}");
			Console.WriteLine($"Wrote YAML files: {result.FilesWritten.Count}");
			Console.WriteLine($"Import warnings: {result.Warnings.Count}");
			Console.WriteLine($"Import dry-run: {result.IsDryRun}");
			if (parsed.UsesInlineConnectionString)
			{
				Console.WriteLine("Security warning: inline connection string usage may expose secrets in shell history.");
			}

			return 0;
		}
		catch (OperationCanceledException)
		{
			Console.Error.WriteLine($"SQL import failed: operation timed out after {parsed.TimeoutSeconds} seconds.");
			return 1;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"SQL import failed: {SanitizeExceptionMessage(ex.Message)}");
			return 1;
		}
	}

	private static int RunValidate(string repoRoot)
	{
		var loadResult = CatalogLoader.Load(repoRoot);
		PrintLines(loadResult.Diagnostics);

		var errors = CatalogValidator.Validate(loadResult.Tables).ToList();
		PrintLines(errors);

		var graph = CatalogGraphLoader.Load(repoRoot);
		PrintLines(graph.Diagnostics);
		var graphErrors = CatalogGraphValidator.Validate(graph).ToList();
		PrintLines(graphErrors);

		return loadResult.Diagnostics.Count == 0
			&& errors.Count == 0
			&& graph.Diagnostics.Count == 0
			&& graphErrors.Count == 0
			? 0
			: 1;
	}

	private static int RunLint(string repoRoot)
	{
		var loadResult = CatalogLoader.Load(repoRoot);
		PrintLines(loadResult.Diagnostics);
		if (loadResult.Diagnostics.Count > 0)
		{
			return 1;
		}

		var policyLoad = GovernancePolicyLoader.Load(repoRoot);
		PrintLines(policyLoad.Diagnostics);

		var warnings = GovernanceEngine.Lint(loadResult.Tables, policyLoad.Policy).ToList();
		PrintLines(warnings);

		var graph = CatalogGraphLoader.Load(repoRoot);
		PrintLines(graph.Diagnostics);
		var graphWarnings = GovernanceEngine.LintGraph(graph).ToList();
		PrintLines(graphWarnings);
		return 0;
	}

	private static int RunGenerateAll(string repoRoot)
	{
		var loadResult = CatalogLoader.Load(repoRoot);
		PrintLines(loadResult.Diagnostics);

		var errors = CatalogValidator.Validate(loadResult.Tables).ToList();
		PrintLines(errors);

		var graph = CatalogGraphLoader.Load(repoRoot);
		PrintLines(graph.Diagnostics);
		var graphErrors = CatalogGraphValidator.Validate(graph).ToList();
		PrintLines(graphErrors);

		if (loadResult.Diagnostics.Count > 0 || errors.Count > 0 || graph.Diagnostics.Count > 0 || graphErrors.Count > 0)
		{
			return 1;
		}

		var policyLoad = GovernancePolicyLoader.Load(repoRoot);
		PrintLines(policyLoad.Diagnostics);

		var warnings = GovernanceEngine.Lint(loadResult.Tables, policyLoad.Policy).ToList();
		PrintLines(warnings);

		var er = MermaidGenerator.GenerateEr(loadResult.Tables);
		var outputPath = Path.Combine(repoRoot, "docs", "generated");
		Directory.CreateDirectory(outputPath);
		var erPath = Path.Combine(outputPath, "er.mmd");
		WriteIfChanged(erPath, er);
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

			WriteIfChanged(path, doc.Content);
		}

		Console.WriteLine($"Generated Markdown docs: {docs.Count}");

		var viewpointOutputPath = Path.Combine(outputPath, "viewpoints");
		Directory.CreateDirectory(viewpointOutputPath);
		var generatedViews = 0;
		foreach (var viewpoint in graph.Viewpoints)
		{
			var view = MermaidGenerator.GenerateGraphView(graph, viewpoint);
			var path = Path.Combine(viewpointOutputPath, $"{viewpoint.Id}.mmd");
			WriteIfChanged(path, view);
			generatedViews++;
		}

		Console.WriteLine($"Generated graph viewpoints: {generatedViews}");

		var lineageAssets = LineageGenerator.Generate(graph);
		foreach (var asset in lineageAssets)
		{
			var path = Path.Combine(outputPath, asset.RelativePath);
			var directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrWhiteSpace(directory))
			{
				Directory.CreateDirectory(directory);
			}

			WriteIfChanged(path, asset.Content);
		}

		Console.WriteLine($"Generated lineage diagrams: {lineageAssets.Count}");

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

			WriteIfChanged(path, asset.Content);
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
		Console.WriteLine("import-sqlserver args: [--dry-run] [--timeout-seconds <n>] (<connectionString> | --connection-env <name> | --connection-file <path>) [repoRoot]");
	}

	private static string SanitizeExceptionMessage(string message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return "unknown error";
		}

		var sanitized = message;
		sanitized = RedactConnectionToken(sanitized, "Password");
		sanitized = RedactConnectionToken(sanitized, "Pwd");
		sanitized = RedactConnectionToken(sanitized, "User ID");
		sanitized = RedactConnectionToken(sanitized, "Uid");
		return sanitized;
	}

	private static string RedactConnectionToken(string input, string key)
	{
		var pattern = $"{Regex.Escape(key)}\\s*=\\s*[^;\\r\\n]*";
		return Regex.Replace(input, pattern, $"{key}=***", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	}

	private static void WriteIfChanged(string path, string content)
	{
		if (File.Exists(path))
		{
			var existing = File.ReadAllText(path);
			if (string.Equals(existing, content, StringComparison.Ordinal))
			{
				return;
			}
		}

		File.WriteAllText(path, content);
	}
}
