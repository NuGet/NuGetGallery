// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit.Abstractions;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.ChildProcess
{
    /// <summary>
    /// Represents a class to run an executable and capture the output and error streams.
    /// </summary>
    public class CommandRunner
    {
        /// <summary>
        /// Runs the specified executable and returns the result.
        /// </summary>
        /// <param name="filename">The path to the executable to run.</param>
        /// <param name="workingDirectory">An optional working directory to use when running the executable.</param>
        /// <param name="arguments">Optional command-line arguments to pass to the executable.</param>
        /// <param name="timeOutInMilliseconds">Optional amount of milliseconds to wait for the executable to exit before returning.</param>
        /// <param name="inputAction">An optional <see cref="Action{T}" /> to invoke against the executables input stream.</param>
        /// <param name="environmentVariables">An optional <see cref="Dictionary{TKey, TValue}" /> containing environment variables to specify when running the executable.</param>
        /// <param name="testOutputHelper">An optional <see cref="ITestOutputHelper" /> to write output to.</param>
        /// <param name="timeoutRetryCount">An optional number of times to retry running the command if it times out. Defaults to 1.</param>
        /// <returns>A <see cref="CommandRunnerResult" /> containing details about the result of the running the executable including the exit code and console output.</returns>
        public static CommandRunnerResult Run(string filename, string workingDirectory = null, string arguments = null, int timeOutInMilliseconds = 60000, Action<StreamWriter> inputAction = null, IDictionary<string, string> environmentVariables = null, ITestOutputHelper testOutputHelper = null, int timeoutRetryCount = 1)
        {
            if (workingDirectory is null)
            {
                workingDirectory = Environment.CurrentDirectory;
            }
            workingDirectory = Path.GetFullPath(workingDirectory);

            if (!Directory.Exists(workingDirectory))
            {
                throw new DirectoryNotFoundException($"The working directory '{workingDirectory}' does not exist.");
            }

            return RetryRunner.RunWithRetries<CommandRunnerResult, TimeoutException>(() =>
            {
                StringBuilder output = new();
                StringBuilder error = new();
                int exitCode = 0;

                using (Process process = new()
                {
                    EnableRaisingEvents = true,
                    StartInfo = new ProcessStartInfo(Path.GetFullPath(filename), arguments)
                    {
                        WorkingDirectory = workingDirectory,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        RedirectStandardInput = true,
                        CreateNoWindow = true,
                    },
                })
                {
                    process.StartInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
                    process.StartInfo.Environment["NUGET_SHOW_STACK"] = bool.TrueString;
                    process.StartInfo.Environment["NuGetTestModeEnabled"] = bool.TrueString;
                    process.StartInfo.Environment["UseSharedCompilation"] = bool.FalseString;
                    process.StartInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = bool.TrueString;
                    process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = bool.TrueString;
                    process.StartInfo.Environment["SuppressNETCoreSdkPreviewMessage"] = bool.TrueString;

                    if (environmentVariables != null)
                    {
                        foreach (var pair in environmentVariables)
                        {
                            process.StartInfo.EnvironmentVariables[pair.Key] = pair.Value;
                        }
                    }

                    process.OutputDataReceived += OnOutputDataReceived;
                    process.ErrorDataReceived += OnErrorDataReceived;

                    testOutputHelper?.WriteLine($"> {process.StartInfo.FileName} {process.StartInfo.Arguments}");

                    Stopwatch stopwatch = Stopwatch.StartNew();

                    process.Start();

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    inputAction?.Invoke(process.StandardInput);

                    process.StandardInput.Close();

                    if (!process.WaitForExit(timeOutInMilliseconds))
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }

                        throw new TimeoutException($"{process.StartInfo.FileName} {process.StartInfo.Arguments} timed out after {stopwatch.Elapsed.TotalSeconds:N2} seconds");
                    }

                    // The application that is processing the asynchronous output should call the WaitForExit method to ensure that the output buffer has been flushed.
                    process.WaitForExit();

                    stopwatch.Stop();
                    testOutputHelper?.WriteLine($"└ Completed in {stopwatch.Elapsed.TotalSeconds:N2}s");

                    process.OutputDataReceived -= OnOutputDataReceived;
                    process.ErrorDataReceived -= OnErrorDataReceived;

                    testOutputHelper?.WriteLine(string.Empty);
                    exitCode = process.ExitCode;
                }

                return new CommandRunnerResult(exitCode, output.ToString(), error.ToString());

                void OnOutputDataReceived(object sender, DataReceivedEventArgs args)
                {
                    if (args?.Data != null)
                    {
                        testOutputHelper?.WriteLine($"│  {args.Data}");

                        lock (output)
                        {
                            output.AppendLine(args.Data);
                        }
                    }
                }

                void OnErrorDataReceived(object sender, DataReceivedEventArgs args)
                {
                    if (args?.Data != null)
                    {
                        testOutputHelper?.WriteLine($"│  {args.Data}");

                        lock (error)
                        {
                            error.AppendLine(args.Data);
                        }
                    }
                }
            },
            timeoutRetryCount,
            testOutputHelper);
        }
    }
}
