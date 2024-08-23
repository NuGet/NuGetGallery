// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace Validation.Symbols.Tests
{
    public sealed class TestDirectory : IDisposable
    {
        public string FullPath { get; }

        private TestDirectory(string fullPath)
        {
            FullPath = fullPath;
        }

        public void Dispose()
        {
            DeleteDirectory(FullPath);
        }

        public static TestDirectory Create()
        {
            var baseDirectoryPath = Path.Combine(Path.GetTempPath(), "NuGetTestFolder");
            var subdirectoryName = Guid.NewGuid().ToString();
            var fullPath = Path.Combine(baseDirectoryPath, subdirectoryName);

            Directory.CreateDirectory(fullPath);

            return new TestDirectory(fullPath);
        }

        public static implicit operator string(TestDirectory directory)
        {
            return directory.FullPath;
        }

        public override string ToString()
        {
            return FullPath;
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, recursive: true);
                }
                catch
                {
                }
            }
        }
    }
}
