// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Jobs.Validation
{
    public class TempFile : ITempFile
    {
        public TempFile()
        {
            FullName = Path.GetTempFileName();
        }

        private TempFile(string fullName)
        {
            FullName = fullName;
        }

        /// <summary>
        /// Creates temp file under specified subdirectory in the temp directory.
        /// Creates missing directories as needed.
        /// </summary>
        /// <param name="directoryName">Subdirectory name under temp directory to create file in.</param>
        public static TempFile Create(string directoryName)
        {
            if (directoryName == null)
            {
                throw new ArgumentNullException(nameof(directoryName));
            }

            if (PathUtility.IsFilePathAbsolute(directoryName))
            {
                throw new ArgumentException($"Directory name must be a relative path, passed value: {directoryName}", nameof(directoryName));
            }

            var directory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), directoryName));
            if (!directory.Exists)
            {
                directory.Create();
            }

            string fileName;
            do
            {
                fileName = Path.Combine(directory.FullName, $"{Guid.NewGuid()}.tmp");
            } while (File.Exists(fileName));
            File.Create(fileName).Dispose();

            return new TempFile(fileName);
        }

        public string FullName { get; }

        public void Dispose()
        {
            try
            {
                // we'll try to delete file if it exists...
                if (File.Exists(FullName))
                {
                    File.Delete(FullName);
                }
            }
            catch
            {
                // ... but won't throw if anything goes wrong
            }
        }
    }
}
