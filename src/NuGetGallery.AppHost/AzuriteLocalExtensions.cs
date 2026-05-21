// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;

/// <summary>
/// Extension methods to add Azurite as a local executable resource instead of a Docker container.
/// Priority order:
/// 1. VS-bundled Azurite executable (no Node.js or Docker required)
/// 2. npx azurite (requires Node.js, no Docker required)
/// 3. Docker container emulator (fallback)
/// See: https://github.com/microsoft/aspire/issues/6673
/// </summary>
public static class AzuriteLocalExtensions
{
	/// <summary>
	/// Fluent helper: adds a <c>.WaitFor()</c> dependency on the Azurite resource
	/// (whether it's a local executable or a container emulator).
	/// </summary>
	public static IResourceBuilder<T> WaitFor<T>(
		this IResourceBuilder<T> builder, AzuriteResult azurite)
		where T : IResourceWithWaitSupport
	{
		return azurite.ApplyWaitFor(builder);
	}

	/// <summary>
	/// Adds Azurite storage to the application. Tries sources in order:
	/// 1. VS-bundled Azurite executable (fastest, no dependencies)
	/// 2. npx azurite (requires Node.js on PATH, downloads azurite on first use)
	/// 3. Docker container emulator (requires Docker Desktop)
	/// </summary>
	public static AzuriteResult AddLocalOrEmulatorAzurite(
		this IDistributedApplicationBuilder builder,
		string dataPath = "./azurite-data",
		int blobPort = 10000,
		int queuePort = 10001,
		int tablePort = 10002)
	{
		var workingDir = Path.GetFullPath(
			Path.Combine(builder.AppHostDirectory, dataPath));
		Directory.CreateDirectory(workingDir);

		string[] azuriteArgs =
		[
			"--blobPort", blobPort.ToString(),
			"--queuePort", queuePort.ToString(),
			"--tablePort", tablePort.ToString(),
			"--location", workingDir,
			"--skipApiVersionCheck",
		];

		// 1. Try VS-bundled Azurite
		var vsAzurite = FindVsAzurite();
		if (vsAzurite is not null)
		{
			Console.WriteLine($"[Azurite] Using VS-bundled executable: {vsAzurite}");
			var resource = builder.AddExecutable("storage", vsAzurite, workingDir, azuriteArgs)
				.ExcludeFromManifest();
			return new AzuriteResult(resource);
		}

		// 2. Try npx (ships with Node.js)
		var npxPath = FindNpx();
		if (npxPath is not null)
		{
			Console.WriteLine($"[Azurite] Using npx: {npxPath}");
			// npx --yes azurite <args>: --yes skips the install prompt
			string[] npxArgs = ["--yes", "azurite", .. azuriteArgs];
			var resource = builder.AddExecutable("storage", npxPath, workingDir, npxArgs)
				.ExcludeFromManifest();
			return new AzuriteResult(resource);
		}

		// 3. Fall back to Docker container
		Console.WriteLine("[Azurite] No local Azurite or npx found. Falling back to Docker container.");
		var emulator = builder.AddAzureStorage("storage")
			.RunAsEmulator(r => r
				.WithDataBindMount(dataPath)
				.WithBlobPort(blobPort)
				.WithQueuePort(queuePort)
				.WithTablePort(tablePort));
		emulator.AddBlobs("blobs");

		return new AzuriteResult(emulator);
	}

	/// <summary>
	/// Locates the Azurite executable shipped with Visual Studio.
	/// </summary>
	private static string? FindVsAzurite()
	{
		var vsWherePaths = new[]
		{
			Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
				@"Microsoft Visual Studio\Installer\vswhere.exe"),
			Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
				@"Microsoft Visual Studio\Installer\vswhere.exe"),
		};

		var vsWhere = vsWherePaths.FirstOrDefault(File.Exists);
		if (vsWhere is null)
		{
			return null;
		}

		var psi = new System.Diagnostics.ProcessStartInfo(vsWhere, "-latest -prerelease -property installationPath")
		{
			RedirectStandardOutput = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};
		using var proc = System.Diagnostics.Process.Start(psi);
		var vsInstallDir = proc?.StandardOutput.ReadToEnd().Trim();
		proc?.WaitForExit();

		if (string.IsNullOrEmpty(vsInstallDir))
		{
			return null;
		}

		var azuritePath = Path.Combine(vsInstallDir,
			@"Common7\IDE\Extensions\Microsoft\Azure Storage Emulator\azurite.exe");

		return File.Exists(azuritePath) ? azuritePath : null;
	}

	/// <summary>
	/// Locates npx on the system PATH. Returns the full path to npx.cmd (Windows)
	/// or npx (Unix), or null if Node.js is not installed.
	/// </summary>
	private static string? FindNpx()
	{
		// On Windows, npx ships as npx.cmd alongside node.exe
		var npxName = OperatingSystem.IsWindows() ? "npx.cmd" : "npx";

		var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
		foreach (var dir in pathDirs)
		{
			var candidate = Path.Combine(dir, npxName);
			if (File.Exists(candidate))
			{
				return candidate;
			}
		}

		return null;
	}
}

/// <summary>
/// Result of <see cref="AzuriteLocalExtensions.AddLocalOrEmulatorAzurite"/> providing
/// access to the storage resource regardless of whether local exe or container was used.
/// The <see cref="AzuriteLocalExtensions.WaitFor"/> extension method on <c>IResourceBuilder</c>
/// uses this to add a dependency on whichever Azurite resource was created.
/// </summary>
public class AzuriteResult
{
	private readonly IResourceBuilder<ExecutableResource>? _executableBuilder;
	private readonly IResourceBuilder<AzureStorageResource>? _emulatorBuilder;

	internal AzuriteResult(IResourceBuilder<ExecutableResource> executableBuilder)
	{
		_executableBuilder = executableBuilder;
	}

	internal AzuriteResult(IResourceBuilder<AzureStorageResource> emulatorBuilder)
	{
		_emulatorBuilder = emulatorBuilder;
	}

	/// <summary>True if using the local VS Azurite executable.</summary>
	public bool IsLocalExecutable => _executableBuilder is not null;

	/// <summary>The underlying resource (for calling Aspire APIs that need the resource directly).</summary>
	public IResource Resource => (IResource?)_executableBuilder?.Resource ?? _emulatorBuilder!.Resource;

	/// <summary>
	/// Applies <c>.WaitFor()</c> on the given resource builder, targeting whichever
	/// Azurite resource was created (executable or container).
	/// Returns the dependent builder for fluent chaining.
	/// </summary>
	internal IResourceBuilder<T> ApplyWaitFor<T>(IResourceBuilder<T> dependent) where T : IResourceWithWaitSupport
	{
		if (_executableBuilder is not null)
			dependent.WaitFor(_executableBuilder);
		else
			dependent.WaitFor(_emulatorBuilder!);
		return dependent;
	}
}
