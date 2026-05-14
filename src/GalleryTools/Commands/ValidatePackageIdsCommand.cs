// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery;
using NuGetGallery.Packaging;

namespace GalleryTools.Commands
{
	public static class ValidatePackageIdsCommand
	{
		private const string PathOption = "--path";
		private const string OutputOption = "--output";

		public static void Configure(CommandLineApplication config)
		{
			config.Description = "Validate package IDs from a CSV file against the package ID normalization validation rules";
			config.HelpOption("-? | -h | --help");

			var pathOption = config.Option(
				$"-p | {PathOption}",
				"Path to a CSV file with an 'IdVariant' column containing package IDs to validate",
				CommandOptionType.SingleValue);

			var outputOption = config.Option(
				$"-o | {OutputOption}",
				"Optional: Path to output CSV file with validation results. If not specified, results are written to console.",
				CommandOptionType.SingleValue);

			config.OnExecute(() =>
			{
				return Execute(pathOption, outputOption);
			});
		}

		private static int Execute(CommandOption pathOption, CommandOption outputOption)
		{
			if (!pathOption.HasValue())
			{
				Console.WriteLine($"The '{PathOption}' parameter is required.");
				return 1;
			}

			var inputPath = pathOption.Value();
			if (!File.Exists(inputPath))
			{
				Console.WriteLine($"File not found: {inputPath}");
				return 1;
			}

			Console.WriteLine($"Reading package IDs from: {inputPath}");

			var results = new List<ValidationResult>();
			var validCount = 0;
			var invalidCount = 0;

			try
			{
				using (var reader = new StreamReader(inputPath, Encoding.UTF8))
				{
					string line;
					int lineNumber = 0;

					// Read header
					line = reader.ReadLine();
					lineNumber++;

					if (line == null || !line.Contains("IdVariant"))
					{
						Console.WriteLine("Error: CSV file must have an 'IdVariant' column header");
						return 1;
					}

					// Find the index of IdVariant column
					var headers = line.Split(',');
					var idVariantIndex = Array.FindIndex(headers, h => h.Trim() == "IdVariant");
					if (idVariantIndex < 0)
					{
						Console.WriteLine("Error: 'IdVariant' column not found in CSV header");
						return 1;
					}

					// Process data rows
					while ((line = reader.ReadLine()) != null)
					{
						lineNumber++;

						if (string.IsNullOrWhiteSpace(line))
							continue;

						var columns = ParseCsvLine(line);
						if (columns.Count <= idVariantIndex)
							continue;

						var packageId = columns[idVariantIndex].Trim();
						if (string.IsNullOrEmpty(packageId))
							continue;

						var validationResult = ValidatePackageId(packageId);
						results.Add(validationResult);

						if (validationResult.IsValid)
							validCount++;
						else
							invalidCount++;

						// Progress indicator
						if (results.Count % 100 == 0)
							Console.Write(".");
					}

					Console.WriteLine();
					Console.WriteLine($"Processed {results.Count} package IDs");
					Console.WriteLine($"Valid: {validCount}, Invalid: {invalidCount}");
				}

				// Output results
				if (outputOption.HasValue())
				{
					var outputPath = outputOption.Value();
					WriteResultsToFile(outputPath, results);
					Console.WriteLine($"Results written to: {outputPath}");
				}
				else
				{
					WriteResultsToConsole(results);
				}

				return 0;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error processing file: {ex.Message}");
				return 1;
			}
		}

		private static ValidationResult ValidatePackageId(string packageId)
		{
			var result = new ValidationResult { PackageId = packageId };

			try
			{
				var packageMetadata = new PackageMetadata(
                    new Dictionary<string, string> { { "id", packageId }, { "version", "1.0.0" } },
                    [], [], [], null, null, null);
				var validationResult = PackageMetadataValidationService.CheckPackageIdForBannedCharacters(
                    packageMetadata,
                    new PackageIdentity(packageId, new NuGetVersion("1.0.0")));

				if (validationResult != null)
				{
					result.IsValid = false;
					result.ErrorMessage = validationResult.Message?.PlainTextMessage ?? "Invalid package ID";
				}
				else
				{
					result.IsValid = true;
					result.ErrorMessage = null;
				}

				return result;
			}
			catch (ArgumentNullException ex)
			{
				result.IsValid = false;
				result.ErrorMessage = ex.Message;
				return result;
			}
			catch (Exception ex)
			{
				result.IsValid = false;
				result.ErrorMessage = $"Exception: {ex.Message}";
				return result;
			}
		}

		private static List<string> ParseCsvLine(string line)
		{
			var result = new List<string>();
			var current = new StringBuilder();
			var inQuotes = false;

			foreach (var c in line)
			{
				if (c == '"')
				{
					inQuotes = !inQuotes;
				}
				else if (c == ',' && !inQuotes)
				{
					result.Add(current.ToString());
					current.Clear();
				}
				else
				{
					current.Append(c);
				}
			}

			result.Add(current.ToString());
			return result;
		}

		private static void WriteResultsToConsole(List<ValidationResult> results)
		{
			var invalidResults = results.Where(r => !r.IsValid).ToList();

			if (invalidResults.Any())
			{
				Console.WriteLine("\n=== INVALID PACKAGE IDs ===");
				foreach (var result in invalidResults.Take(50))
				{
					Console.WriteLine($"ID: {result.PackageId}");
					Console.WriteLine($"   Error: {result.ErrorMessage}\n");
				}

				if (invalidResults.Count > 50)
					Console.WriteLine($"... and {invalidResults.Count - 50} more invalid IDs");
			}
			else
			{
				Console.WriteLine("\n=== All package IDs are VALID ===");
			}
		}

		private static void WriteResultsToFile(string outputPath, List<ValidationResult> results)
		{
			using (var writer = new StreamWriter(outputPath, false, Encoding.UTF8))
			{
				// Write header
				writer.WriteLine("PackageId,IsValid,ErrorMessage");

				// Write results
				foreach (var result in results)
				{
					var csvLine = $"\"{EscapeCsvField(result.PackageId)}\",{result.IsValid},{EscapeCsvField(result.ErrorMessage ?? "")}";
					writer.WriteLine(csvLine);
				}
			}
		}

		private static string EscapeCsvField(string field)
		{
			if (string.IsNullOrEmpty(field))
				return "";

			return field.Replace("\"", "\"\"");
		}

		private class ValidationResult
		{
			public string PackageId { get; set; }
			public bool IsValid { get; set; }
			public string ErrorMessage { get; set; }
		}
	}
}
