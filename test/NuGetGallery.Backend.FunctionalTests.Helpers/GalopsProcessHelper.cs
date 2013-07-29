using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace NuGetOperations.FunctionalTests.Helpers
{
    public class GalopsProcessHelper
    {
        #region PublicMethods

        /// <summary>
        /// Invokes galops.exe with the appropriate parameters.
        /// </summary>
        /// <param name="arguments">cmd line args to galops.exe</param>
        /// <param name="standardError">stderror from the galops process</param>
        /// <param name="standardOutput">stdoutput from the galops process</param>
        /// <param name="WorkingDir">working dir if any to be used</param>
        /// <returns></returns>
        public static int InvokeGalopsProcess(string arguments, out string standardError, out string standardOutput, string WorkingDir = null)
        {
            Process galOpsProcess = new Process();
            ProcessStartInfo galopsProcessStartInfo = new ProcessStartInfo(Path.Combine(Environment.CurrentDirectory,GalopsExePath));
            galopsProcessStartInfo.Arguments = arguments;
            galopsProcessStartInfo.RedirectStandardError = true;
            galopsProcessStartInfo.RedirectStandardOutput = true;
            galopsProcessStartInfo.RedirectStandardInput = true;
            galopsProcessStartInfo.UseShellExecute = false;
            galopsProcessStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            galopsProcessStartInfo.CreateNoWindow = true;
            galOpsProcess.StartInfo = galopsProcessStartInfo;
            galOpsProcess.StartInfo.WorkingDirectory = WorkingDir;
            galOpsProcess.Start();           
            standardError = galOpsProcess.StandardError.ReadToEnd();
            standardOutput = galOpsProcess.StandardOutput.ReadToEnd();
            Console.WriteLine(standardError);
            Console.WriteLine(standardOutput);
            galOpsProcess.WaitForExit();
            return galOpsProcess.ExitCode;
        }

        #endregion PublicMethods

        #region PrivateVariables

        public const string GalopsExePath = "galops.exe";
        public const string ConnectionStringCommand = " -ConnectionString ";
        public const string WhatIfCommand = "-WhatIf ";
        public const string IfOlderThanCommand = "-IfOlderThan ";
        public const string BackupDataBaseCommand = " bdb ";

        #endregion PrivateVariables
    }
}
