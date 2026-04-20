// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;

/// <summary>
/// Extension methods to add Azurite as a local executable resource instead of a Docker container.
/// Uses the Azurite installation bundled with Visual Studio when available.
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
	/// Adds Azurite storage to the application. Prefers the local VS-bundled Azurite executable
	/// (no Docker required); falls back to the container-based emulator if not found.
	/// </summary>
	public static AzuriteResult AddLocalOrEmulatorAzurite(
		this IDistributedApplicationBuilder builder,
		string dataPath = "./azurite-data",
		int blobPort = 10000,
		int queuePort = 10001,
		int tablePort = 10002)
	{
		var azuritePath = FindVsAzurite();

		if (azuritePath is not null)
		{
			var workingDir = Path.GetFullPath(
				Path.Combine(builder.AppHostDirectory, dataPath));
			Directory.CreateDirectory(workingDir);

			var resource = builder.AddExecutable("storage", azuritePath, workingDir,
					"--blobPort", blobPort.ToString(),
					"--queuePort", queuePort.ToString(),
					"--tablePort", tablePort.ToString(),
					"--location", workingDir,
					"--skipApiVersionCheck")
				.ExcludeFromManifest();

			return new AzuriteResult(resource);
		}
		else
		{
			var emulator = builder.AddAzureStorage("storage")
				.RunAsEmulator(r => r
					.WithDataBindMount(dataPath)
					.WithBlobPort(blobPort)
					.WithQueuePort(queuePort)
					.WithTablePort(tablePort));
			emulator.AddBlobs("blobs");

			return new AzuriteResult(emulator);
		}
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
