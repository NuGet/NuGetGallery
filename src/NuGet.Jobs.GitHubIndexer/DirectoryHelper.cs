// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace NuGet.Jobs.GitHubIndexer
{
    /// <summary>
    /// Provides robust directory deletion that handles:
    /// - Read-only files (common in .git folders)
    /// - NTFS race conditions where file deletions are not fully committed
    ///   before the parent directory removal is attempted
    /// - Windows reserved device names (CON, PRN, AUX, NUL, COM1-9, LPT1-9)
    ///   which cannot be deleted through normal .NET APIs
    /// </summary>
    public static class DirectoryHelper
    {
        /// <summary>
        /// Deletes a directory, retrying on transient failures and falling back
        /// to <c>cmd /c rmdir</c> with the <c>\\?\</c> prefix for paths that
        /// contain Windows reserved device names.
        /// </summary>
        public static void DeleteDirectoryWithRetries(string path, ILogger logger, int maxRetries = 3)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            for (var attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    ClearReadOnlyAttributes(path);
                    Directory.Delete(path, recursive: true);
                    return;
                }
                catch (IOException ex) when (attempt < maxRetries)
                {
                    logger.LogWarning (
                        "Failed to delete directory {Path} on attempt {Attempt}: {Message}. Retrying...",
                        path,
                        attempt + 1,
                        ex.Message);
                    Thread.Sleep(200 * (attempt + 1));
                }
                catch (UnauthorizedAccessException ex) when (attempt < maxRetries)
                {
                    logger.LogWarning (
                        "Unauthorized access deleting directory {Path} on attempt {Attempt}: {Message}. Retrying...",
                        path,
                        attempt + 1,
                        ex.Message);
                    Thread.Sleep(200 * (attempt + 1));
                }
                catch (Exception) when (attempt == maxRetries)
                {
                    // All retries exhausted with normal APIs. Fall back to
                    // cmd /c rmdir with the \\?\ prefix, which bypasses
                    // Windows reserved device name restrictions (AUX, CON, etc.).
                    logger.LogWarning (
                        "Normal deletion failed for {Path} after {MaxRetries} retries. Falling back to rmdir with UNC prefix.",
                        path,
                        maxRetries);
                    DeleteWithRmdir(path, logger);
                    return;
                }
            }
        }

        /// <summary>
        /// Clears read-only attributes on all files in a directory tree.
        /// Skips files that cannot be accessed (e.g. due to reserved names).
        /// </summary>
        private static void ClearReadOnlyAttributes (string path)
        {
            try
            {
                var dirInfo = new DirectoryInfo(path);
                foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        file.IsReadOnly = false;
                    }
                    catch (Exception)
                    {
                        // Skip files that can't be accessed (reserved names, etc.)
                    }
                }
            }
            catch (Exception)
            {
                // Skip if enumeration itself fails
            }
        }

        /// <summary>
        /// Deletes a directory using <c>cmd /c rmdir /s /q</c> with the <c>\\?\</c>
        /// extended-length path prefix. This bypasses Windows reserved device name
        /// validation and handles long paths.
        /// </summary>
        private static void DeleteWithRmdir(string path, ILogger logger)
        {
            var fullPath = Path.GetFullPath(path);
            var uncPath = @"\\?\" + fullPath;

            var processInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c rmdir /s /q \"{uncPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };

            try
            {
                using (var process = Process.Start(processInfo))
                {
                    var stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit ();

                    if (process.ExitCode != 0 && Directory.Exists(path))
                    {
                        logger.LogError(
                            "rmdir fallback failed for {Path} with exit code {ExitCode}: {Error}",
                            path,
                            process.ExitCode,
                            stderr);
                    }
                    else
                    {
                        logger.LogInformation("Successfully deleted {Path} using rmdir fallback.", path);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to launch rmdir fallback for {Path}.", path);
            }
        }
    }
}
