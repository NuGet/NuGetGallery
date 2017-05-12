using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.CodeDom.Compiler;
using Microsoft.CSharp;

namespace NugetClientSDKHelpers
{
    /// <summary>
    /// Provides helpers functions around NuGet.exe
    /// </summary>
    public class CmdLineHelper
    {

        #region PublicMethods
        /// <summary>
        /// Creates a package given the nuspec file.
        /// </summary>
        /// <param name="nuspecFileFullPath">NuspecFile based on which the package has to be created.</param>
        /// <param name="destinationPackagePath">Output directory for the new package</param>
        public static string CreatePackage(string packageName)
        {
            string standardOutput = string.Empty;
            string standardError = string.Empty;
            string nuspecFileFullPath = CreateDefaultNuspecFile(packageName);
            string nuspecDir = Path.GetDirectoryName(nuspecFileFullPath);
            AddContent(nuspecDir);
            AddLib(nuspecDir);
            InvokeNugetProcess(string.Join(string.Empty, new string[] { PackCommandString, @"""" + nuspecFileFullPath + @"""", OutputDirectorySwitchString, @"""" +nuspecDir + @"""" }), out standardError, out standardOutput, Path.GetFullPath(Path.GetDirectoryName(nuspecFileFullPath)));
            string[] nupkgFiles = Directory.GetFiles(nuspecDir, "*.nupkg").ToArray();
            if (nupkgFiles == null || nupkgFiles.Length == 0)
                return null;
            else
                return nupkgFiles[0];
        }

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
            return InvokeNugetProcess(string.Join(string.Empty, new string[] { PushCommandString, @"""" + packageFullPath + @"""",SourceSwitchString, sourceName }), out standardError, out standardOutput);            
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
        /// Creates a Nuspec file given the Package Name.
        /// </summary>
        /// <param name="packageName"></param>
        /// <returns></returns>
        internal static string CreateDefaultNuspecFile(string packageName)
        {
            string standardOutput = string.Empty;
            string standardError = string.Empty;
            string packageDir = Path.Combine(Environment.CurrentDirectory, packageName);
            if (Directory.Exists(packageDir))
                Directory.Delete(packageDir, true);
            Directory.CreateDirectory(packageDir);
            InvokeNugetProcess(string.Join(string.Empty, new string[] { SpecCommandString, packageName }), out standardError, out standardOutput, packageDir);
            return Path.Combine(packageDir, packageName + ".nuspec");

        }

        /// <summary>
        /// Adds default contents dir in the nuspec file location.
        /// </summary>
        /// <param name="nuspecFileDir"></param>
        internal static void AddContent(string nuspecFileDir)
        {
            string contentsDir = Path.Combine(nuspecFileDir, "contents");
            Directory.CreateDirectory(contentsDir);
            StreamWriter sw = new StreamWriter(Path.Combine(contentsDir, @"Samplecontent.txt"));
            sw.Flush();
            sw.Close();
        }

        /// <summary>
        /// Adds default Lib dir by adding a codedom generated assembly.
        /// </summary>
        /// <param name="nuspecFileDir"></param>
        internal static void AddLib(string nuspecFileDir)
        {
            Directory.CreateDirectory(Path.Combine(nuspecFileDir, "Lib"));
            System.CodeDom.Compiler.CompilerParameters parameters = new CompilerParameters();
            parameters.GenerateExecutable = false;
            parameters.CompilerOptions = "/optimize /unsafe";
            parameters.OutputAssembly = (Path.Combine(nuspecFileDir, @"Lib", DateTime.Now.Ticks.ToString() + ".dll"));
            CSharpCodeProvider provider = new CSharpCodeProvider();
            string source = "using System; namespace CodeDom { public class B {public static int k=7;}}";
            CompilerResults r = provider.CompileAssemblyFromSource(parameters, source);

        }

        /// <summary>
        /// Invokes nuget.exe with the appropriate parameters.
        /// </summary>
        /// <param name="arguments">cmd line args to NuGet.exe</param>
        /// <param name="standardError">stderror from the nuget process</param>
        /// <param name="standardOutput">stdoutput from the nuget process</param>
        /// <param name="WorkingDir">working dir if any to be used</param>
        /// <returns></returns>
        private static int InvokeNugetProcess(string arguments, out string standardError, out string standardOutput, string WorkingDir = null)
        {
            Process nugetProcess = new Process();
            ProcessStartInfo nugetProcessStartInfo = new ProcessStartInfo(Path.Combine(Environment.CurrentDirectory, NugetExePath));
            nugetProcessStartInfo.Arguments = arguments;
            nugetProcessStartInfo.RedirectStandardError = true;
            nugetProcessStartInfo.RedirectStandardOutput = true;
            nugetProcessStartInfo.RedirectStandardInput = true;
            nugetProcessStartInfo.UseShellExecute = false;
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
        #endregion PrivateMemebers
    }
}
