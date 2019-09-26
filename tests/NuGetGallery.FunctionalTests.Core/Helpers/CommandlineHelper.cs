// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests
{
    /// <summary>
    /// Provides helpers functions around NuGet.exe
    /// </summary>
    public class CommandlineHelper
        : HelperBase
    {
        internal static string AnalyzeCommandString = "analyze";
        internal static string SpecCommandString = "spec -force";
        internal static string PackCommandString = "pack";
        internal static string UpdateCommandString = "update";
        internal static string InstallCommandString = "install";
        internal static string DeleteCommandString = "delete";
        internal static string PushCommandString = "push";
        internal static string OutputDirectorySwitchString = "-OutputDirectory";
        internal static string PreReleaseSwitchString = "-Prerelease";
        internal static string SourceSwitchString = "-Source";
        internal static string ApiKeySwitchString = "-ApiKey";
        internal static string SelfSwitch = "-self";
        internal static string NonInteractiveSwitchString = "-noninteractive";
        internal static string ExcludeVersionSwitchString = "-ExcludeVersion";
        internal static string NugetExePath = @"NuGet.exe";
        internal static string SampleDependency = "SampleDependency";
        internal static string SampleDependencyVersion = "1.0";

        public CommandlineHelper(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        /// <summary>
        /// Uploads the given package to the specified source and returns the exit code.
        /// </summary>
        /// <param name="packageFullPath"></param>
        /// <param name="sourceName"></param>
        /// <returns></returns>
        public async Task<ProcessResult> UploadPackageAsync(string packageFullPath, string sourceName, string apiKey = null)
        {
            string message = $"Uploading package {packageFullPath} to {sourceName}.";

            if (apiKey == null)
            {
                apiKey = GalleryConfiguration.Instance.Account.ApiKey;

                message += " Using full access API key";
            }

            WriteLine(message);

            var arguments = new List<string>
            {
                PushCommandString, packageFullPath, SourceSwitchString, sourceName, ApiKeySwitchString, apiKey
            };

            return await InvokeNugetProcess(arguments);
        }

        /// <summary>
        ///  Delete the specified package using Nuget.exe
        /// </summary>
        /// <param name="packageId">package to be deleted</param>
        /// <param name="version">version of package to be deleted</param>
        /// <param name="sourceName">source url</param>
        /// <returns></returns>
        public async Task<ProcessResult> DeletePackageAsync(string packageId, string version, string sourceName, string apiKey = null)
        {
            string message = $"Deleting package {packageId} with version {version} from {sourceName}.";

            if (apiKey == null)
            {
                apiKey = GalleryConfiguration.Instance.Account.ApiKey;

                message += " Using full access API key";
            }

            WriteLine(message);

            var arguments = new List<string>
            {
                DeleteCommandString, packageId, version, SourceSwitchString, sourceName, ApiKeySwitchString, apiKey
            };
            return await InvokeNugetProcess(arguments);
        }

        /// <summary>
        ///  Install the specified package using Nuget.exe
        /// </summary>
        /// <param name="packageId">package to be installed</param>
        /// <param name="sourceName">source url</param>
        /// <returns></returns>
        public async Task<ProcessResult> InstallPackageAsync(string packageId, string sourceName)
        {
            WriteLine("Installing package " + packageId + " from " + sourceName);

            var arguments = new List<string>
            {
                InstallCommandString, packageId, SourceSwitchString, sourceName
            };
            return await InvokeNugetProcess(arguments);
        }

        /// <summary>
        ///  Install the specified package using Nuget.exe, specifying the output directory
        /// </summary>
        /// <param name="packageId">package to be installed</param>
        /// <param name="sourceName">source url</param>
        /// <param name="outputDirectory">outputDirectory</param>
        /// <returns></returns>
        public async Task<ProcessResult> InstallPackageAsync(string packageId, string sourceName, string outputDirectory)
        {
            WriteLine("Installing package " + packageId + " from " + sourceName + " to " + outputDirectory);

            var arguments = new List<string>
            {
                InstallCommandString, packageId, SourceSwitchString, sourceName, OutputDirectorySwitchString,
                outputDirectory
            };
            return await InvokeNugetProcess(arguments);
        }

        public async Task<ProcessResult> SpecPackageAsync(string packageName, string packageDir)
        {
            var arguments = new List<string>
            {
                SpecCommandString, packageName
            };
            return await InvokeNugetProcess(arguments, packageDir);
        }

        public async Task<ProcessResult> PackPackageAsync(string nuspecFileFullPath, string nuspecDir)
        {
            var arguments = new List<string>
            {
                PackCommandString, nuspecFileFullPath, OutputDirectorySwitchString, nuspecDir
            };
            return await InvokeNugetProcess(arguments, Path.GetFullPath(Path.GetDirectoryName(nuspecFileFullPath)));
        }

        /// <summary>
        /// Self update on nuget.exe
        /// </summary>
        /// <returns></returns>
        public async Task<ProcessResult> UpdateNugetExeAsync()
        {
            var arguments = new List<string>
            {
                UpdateCommandString, SelfSwitch
            };
            return await InvokeNugetProcess(arguments);

        }

        /// <summary>
        /// Invokes nuget.exe with the appropriate parameters.
        /// </summary>
        /// <param name="arguments">cmd line args to NuGet.exe</param>
        /// <param name="workingDir">working dir if any to be used</param>
        /// <param name="timeout">Timeout in seconds (default = 6min).</param>
        /// <returns></returns>
        public async Task<ProcessResult> InvokeNugetProcess(List<string> arguments, string workingDir = null, int timeout = 360)
        {
            var nugetProcess = new Process();
            var pathToNugetExe = Path.Combine(Environment.CurrentDirectory, NugetExePath);

            arguments.Add(NonInteractiveSwitchString);

            var argumentsString = string.Join(" ", arguments);

            WriteLine("The NuGet.exe command to be executed is: " + pathToNugetExe + " " + argumentsString);

            // During the actual test run, a script will copy the latest NuGet.exe and overwrite the existing one
            ProcessStartInfo nugetProcessStartInfo = new ProcessStartInfo(pathToNugetExe);
            nugetProcessStartInfo.Arguments = argumentsString;
            nugetProcessStartInfo.RedirectStandardError = true;
            nugetProcessStartInfo.RedirectStandardOutput = true;
            nugetProcessStartInfo.RedirectStandardInput = true;
            nugetProcessStartInfo.UseShellExecute = false;
            nugetProcessStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            nugetProcessStartInfo.CreateNoWindow = true;
            nugetProcess.StartInfo = nugetProcessStartInfo;

            if (workingDir != null)
            {
                nugetProcess.StartInfo.WorkingDirectory = workingDir;
            }

            nugetProcess.Start();

            var standardError = await nugetProcess.StandardError.ReadToEndAsync();
            var standardOutput = await nugetProcess.StandardOutput.ReadToEndAsync();


            WriteLine(standardOutput);

            if (!string.IsNullOrEmpty(standardError))
            {
                WriteLine(standardError);
            }

            nugetProcess.WaitForExit(timeout * 1000);

            var processResult = new ProcessResult(nugetProcess.ExitCode, standardError);
            return processResult;
        }

        public sealed class ProcessResult
        {
            public ProcessResult(int exitCode, string standardError)
            {
                ExitCode = exitCode;
                StandardError = standardError;
            }

            public int ExitCode { get; private set; }
            public string StandardError { get; private set; }
        }
    }
}