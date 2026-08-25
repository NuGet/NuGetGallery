// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Xunit;

namespace NuGetGallery.FunctionalTests.Playwright
{
    public sealed class AspirePlaywrightFixture : IAsyncLifetime
    {
        internal const string HarnessEnvironmentVariable = "NUGET_PLAYWRIGHT_ASPIRE_HARNESS";
        private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(10);
        private readonly string _settingsPath = Path.Combine(
            Path.GetTempPath(),
            $"NuGetGallery.FunctionalTests.{Guid.NewGuid():N}.json");

        private DistributedApplication _application;
        private GalleryConfiguration _originalGalleryConfiguration;
        private bool _galleryConfigurationInitialized;
        private string _originalAppHostProfile;
        private string _originalConfigurationFilePath;
        private string _originalHarnessEnvironment;

        public async Task InitializeAsync()
        {
            _originalAppHostProfile = Environment.GetEnvironmentVariable("APPHOST_PROFILE");
            _originalConfigurationFilePath = Environment.GetEnvironmentVariable(
                EnvironmentSettings.ConfigurationFilePathVariableName);
            _originalHarnessEnvironment = Environment.GetEnvironmentVariable(HarnessEnvironmentVariable);
            Environment.SetEnvironmentVariable(HarnessEnvironmentVariable, bool.TrueString);

            if (string.IsNullOrWhiteSpace(_originalAppHostProfile))
            {
                Environment.SetEnvironmentVariable("APPHOST_PROFILE", "ci-gallery");
            }

            try
            {
                using (var timeout = new CancellationTokenSource(StartupTimeout))
                {
                    var builder = await DistributedApplicationTestingBuilder
                        .CreateAsync<Projects.NuGetGallery_AppHost>(timeout.Token);

                    _application = await builder.BuildAsync(timeout.Token);
                    await _application.StartAsync(timeout.Token);
                    await _application.ResourceNotifications.WaitForResourceHealthyAsync(
                        "gallery",
                        timeout.Token);
                }

                await SeedFunctionalTestDataAsync();
                Environment.SetEnvironmentVariable(
                    EnvironmentSettings.ConfigurationFilePathVariableName,
                    _settingsPath);
                _originalGalleryConfiguration = GalleryConfiguration.Initialize(_settingsPath);
                _galleryConfigurationInitialized = true;
            }
            catch
            {
                await DisposeApplicationAsync();
                RestoreGalleryConfiguration();
                RestoreEnvironment();
                DeleteSettingsFile();
                throw;
            }
        }

        public async Task DisposeAsync()
        {
            await DisposeApplicationAsync();
            RestoreEnvironment();
            DeleteSettingsFile();
        }

        private async Task SeedFunctionalTestDataAsync()
        {
            var repositoryRoot = GetRepositoryRoot();
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
                ?? throw new InvalidOperationException(
                    $"Could not determine the build configuration from '{AppContext.BaseDirectory}'.");
            var galleryToolsDirectory = Path.Combine(
                repositoryRoot,
                "src",
                "GalleryTools",
                "bin",
                configuration,
                "net472");
            var galleryToolsPath = Path.Combine(galleryToolsDirectory, "GalleryTools.exe");

            if (!File.Exists(galleryToolsPath))
            {
                throw new FileNotFoundException(
                    "GalleryTools.exe was not built for the functional-test configuration.",
                    galleryToolsPath);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = galleryToolsPath,
                WorkingDirectory = galleryToolsDirectory,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("seedfunctionaltests");
            startInfo.ArgumentList.Add("--output");
            startInfo.ArgumentList.Add(_settingsPath);
            startInfo.ArgumentList.Add("--package-dir");
            startInfo.ArgumentList.Add(Path.Combine(
                repositoryRoot,
                "src",
                "NuGetGallery.AppHost",
                "testdata"));
            startInfo.ArgumentList.Add("--base-url");
            startInfo.ArgumentList.Add("https://localhost");

            using (var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start GalleryTools.exe."))
            {
                var standardOutput = process.StandardOutput.ReadToEndAsync();
                var standardError = process.StandardError.ReadToEndAsync();

                using (var timeout = new CancellationTokenSource(StartupTimeout))
                {
                    try
                    {
                        await process.WaitForExitAsync(timeout.Token);
                    }
                    catch (OperationCanceledException) when (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync();
                        throw new TimeoutException(
                            $"GalleryTools seedfunctionaltests did not finish within {StartupTimeout}.");
                    }
                }

                var output = await standardOutput;
                var error = await standardError;
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"GalleryTools seedfunctionaltests failed with exit code {process.ExitCode}.{Environment.NewLine}" +
                        $"Standard output:{Environment.NewLine}{output}{Environment.NewLine}" +
                        $"Standard error:{Environment.NewLine}{error}");
                }
            }
        }

        private async Task DisposeApplicationAsync()
        {
            if (_application != null)
            {
                await _application.DisposeAsync();
                _application = null;
            }
        }

        private void RestoreEnvironment()
        {
            Environment.SetEnvironmentVariable("APPHOST_PROFILE", _originalAppHostProfile);
            Environment.SetEnvironmentVariable(
                EnvironmentSettings.ConfigurationFilePathVariableName,
                _originalConfigurationFilePath);
            Environment.SetEnvironmentVariable(
                HarnessEnvironmentVariable,
                _originalHarnessEnvironment);
        }

        private void RestoreGalleryConfiguration()
        {
            if (_galleryConfigurationInitialized)
            {
                GalleryConfiguration.Restore(_originalGalleryConfiguration);
                _galleryConfigurationInitialized = false;
            }
        }

        private void DeleteSettingsFile()
        {
            if (File.Exists(_settingsPath))
            {
                File.Delete(_settingsPath);
            }
        }

        private static string GetRepositoryRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null && !File.Exists(Path.Combine(current.FullName, "NuGetGallery.FunctionalTests.sln")))
            {
                current = current.Parent;
            }

            return current?.FullName
                ?? throw new InvalidOperationException(
                    $"Could not find the repository root above '{AppContext.BaseDirectory}'.");
        }
    }
}
