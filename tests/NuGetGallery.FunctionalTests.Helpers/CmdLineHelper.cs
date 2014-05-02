using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.CodeDom.Compiler;
using Microsoft.CSharp;

namespace NuGetGallery.FunctionTests.Helpers
{
    /// <summary>
    /// Provides helpers functions around NuGet.exe
    /// </summary>
    public class CmdLineHelper
    {
        #region PublicMethods
     
        /// <summary>
        /// Uploads the given package to the specified source and returns the exit code.
        /// </summary>
        /// <param name="packageFullPath"></param>
        /// <param name="sourceName"></param>
        /// <returns></returns>
        public static int UploadPackage(string packageFullPath, string sourceName)
        {
            string standardOutput = string.Empty;
            string standardError = string.Empty;
            return InvokeNugetProcess(string.Join(string.Empty, new string[] { PushCommandString, @"""" + packageFullPath + @"""", SourceSwitchString, sourceName }), out standardError, out standardOutput);
        }

        /// <summary>
        /// Uploads the given package to the specified source and returns the exit code.
        /// </summary>
        /// <param name="packageFullPath"></param>
        /// <param name="sourceName"></param>
        /// <returns></returns>
        public static int UploadPackage(string packageFullPath, string sourceName, out string standardOutput, out string standardError)
        {
            return InvokeNugetProcess(string.Join(string.Empty, new string[] { PushCommandString, @"""" + packageFullPath + @"""", SourceSwitchString, sourceName }), out standardError, out standardOutput);         
        }

        /// <summary>
        ///  Install the specified package using Nuget.exe
        /// </summary>
        /// <param name="packageId">package to be installed</param>
        /// <param name="sourceName">source url</param>
        /// <returns></returns>
        public static int InstallPackage(string packageId, string sourceName)
        {
            string standardOutput = string.Empty;
            string standardError = string.Empty;
            return InvokeNugetProcess(string.Join(string.Empty, new string[] { InstallCommandString, packageId, SourceSwitchString, sourceName}), out standardError, out standardOutput);
        }

        /// <summary>
        ///  Install the specified package using Nuget.exe, specifying the output directory
        /// </summary>
        /// <param name="packageId">package to be installed</param>
        /// <param name="sourceName">source url</param>
        /// <param name="outputDirectory">outputDirectory</param>
        /// <returns></returns>
        public static int InstallPackage(string packageId, string sourceName, string outputDirectory)
        {
            string standardOutput = string.Empty;
            string standardError = string.Empty;
            return InvokeNugetProcess(string.Join(string.Empty, new string[] { InstallCommandString, packageId, SourceSwitchString, sourceName, OutputDirectorySwitchString, outputDirectory }), out standardError, out standardOutput);
        }

        /// <summary>
        /// Self update on nuget.exe
        /// </summary>
        /// <returns></returns>
        public static int UpdateNugetExe()
        {
            string standardOutput = string.Empty;
            string standardError = string.Empty;
            return InvokeNugetProcess(string.Join(string.Empty, new string[] {UpdateCommandString }), out standardError, out standardOutput);
           
        }
        #endregion PublicMethods

        #region PrivateMethods      

        /// <summary>
        /// Invokes nuget.exe with the appropriate parameters.
        /// </summary>
        /// <param name="arguments">cmd line args to NuGet.exe</param>
        /// <param name="standardError">stderror from the nuget process</param>
        /// <param name="standardOutput">stdoutput from the nuget process</param>
        /// <param name="WorkingDir">working dir if any to be used</param>
        /// <returns></returns>
        public static int InvokeNugetProcess(string arguments, out string standardError, out string standardOutput, string WorkingDir = null)
        {
            Process nugetProcess = new Process();
            string pathToNugetExe = Path.Combine(Environment.CurrentDirectory, NugetExePath);
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
            nugetProcess.StartInfo.WorkingDirectory = WorkingDir;
            nugetProcess.Start();
            standardError = nugetProcess.StandardError.ReadToEnd();
            standardOutput = nugetProcess.StandardOutput.ReadToEnd();
            Console.WriteLine(standardError);
            Console.WriteLine(standardOutput);
            nugetProcess.WaitForExit();
            return nugetProcess.ExitCode;
        }

        #endregion PrivateMethods

        #region PrivateMemebers
        internal static string AnalyzeCommandString = " analyze ";
        internal static string SpecCommandString = " spec -f ";
        internal static string PackCommandString = " pack ";
        internal static string UpdateCommandString = " update ";
        internal static string InstallCommandString = " install ";
        internal static string PushCommandString = " push ";        
        internal static string OutputDirectorySwitchString = " -OutputDirectory ";
        internal static string PreReleaseSwitchString = " -Prerelease ";
        internal static string SourceSwitchString = " -Source ";
        internal static string APIKeySwitchString = " -ApiKey ";
        internal static string ExcludeVersionSwitchString = " -ExcludeVersion ";
        internal static string NugetExePath = @"NuGet.exe";
        internal static string SampleDependency = "SampleDependency";
        internal static string SampleDependencyVersion = "1.0";
        #endregion PrivateMemebers
    }
}
