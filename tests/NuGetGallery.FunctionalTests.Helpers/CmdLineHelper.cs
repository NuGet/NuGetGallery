using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace NuGetGallery.FunctionTests.Helpers
{
    /// <summary>
    /// Provides helpers functions around NuGet.exe
    /// </summary>
    public class CmdLineHelper
    {
        internal static string AnalyzeCommandString = " analyze ";
        internal static string SpecCommandString = " spec -f ";
        internal static string PackCommandString = " pack ";
        internal static string UpdateCommandString = " update ";
        internal static string InstallCommandString = " install ";
        internal static string PushCommandString = " push ";
        internal static string OutputDirectorySwitchString = " -OutputDirectory ";
        internal static string PreReleaseSwitchString = " -Prerelease ";
        internal static string SourceSwitchString = " -Source ";
        internal static string ApiKeySwitchString = " -ApiKey ";
        internal static string ExcludeVersionSwitchString = " -ExcludeVersion ";
        internal static string NugetExePath = @"NuGet.exe";
        internal static string SampleDependency = "SampleDependency";
        internal static string SampleDependencyVersion = "1.0";

        /// <summary>
        /// Uploads the given package to the specified source and returns the exit code.
        /// </summary>
        /// <param name="packageFullPath"></param>
        /// <param name="sourceName"></param>
        /// <returns></returns>
        public static async Task<ProcessResult> UploadPackageAsync(string packageFullPath, string sourceName)
        {
            Console.WriteLine("Uploading package " + packageFullPath + " to " + sourceName);

            var arguments = string.Join(string.Empty, PushCommandString, @"""" + packageFullPath + @"""", SourceSwitchString, sourceName);
            Console.WriteLine("nuget.exe " + arguments);

            return await InvokeNugetProcess(arguments);
        }

        /// <summary>
        ///  Install the specified package using Nuget.exe
        /// </summary>
        /// <param name="packageId">package to be installed</param>
        /// <param name="sourceName">source url</param>
        /// <returns></returns>
        public static async Task<ProcessResult> InstallPackageAsync(string packageId, string sourceName)
        {
            var arguments = string.Join(string.Empty, InstallCommandString, packageId, SourceSwitchString, sourceName);
            return await InvokeNugetProcess(arguments);
        }

        /// <summary>
        ///  Install the specified package using Nuget.exe, specifying the output directory
        /// </summary>
        /// <param name="packageId">package to be installed</param>
        /// <param name="sourceName">source url</param>
        /// <param name="outputDirectory">outputDirectory</param>
        /// <returns></returns>
        public static async Task<ProcessResult> InstallPackageAsync(string packageId, string sourceName, string outputDirectory)
        {
            var arguments = string.Join(string.Empty, InstallCommandString, packageId, SourceSwitchString, sourceName, OutputDirectorySwitchString, outputDirectory);
            return await InvokeNugetProcess(arguments);
        }

        /// <summary>
        /// Self update on nuget.exe
        /// </summary>
        /// <returns></returns>
        public static async Task<ProcessResult> UpdateNugetExeAsync()
        {
            var arguments = string.Join(string.Empty, UpdateCommandString);
            return await InvokeNugetProcess(arguments);

        }

        /// <summary>
        /// Invokes nuget.exe with the appropriate parameters.
        /// </summary>
        /// <param name="arguments">cmd line args to NuGet.exe</param>
        /// <param name="workingDir">working dir if any to be used</param>
        /// <param name="timeout">Timeout in seconds (default = 5min).</param>
        /// <returns></returns>
        public static async Task<ProcessResult> InvokeNugetProcess(string arguments, string workingDir = null, int timeout = 300)
        {
            var nugetProcess = new Process();
            var pathToNugetExe = Path.Combine(Environment.CurrentDirectory, NugetExePath);

            Console.WriteLine("The NuGet.exe command to be executed is: " + pathToNugetExe + " " + arguments);

            // During the actual test run, a script will copy the latest NuGet.exe and overwrite the existing one
            ProcessStartInfo nugetProcessStartInfo = new ProcessStartInfo(pathToNugetExe);
            nugetProcessStartInfo.Arguments = arguments;
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

            Console.WriteLine(standardError);
            Console.WriteLine(standardOutput);

            nugetProcess.WaitForExit();

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
