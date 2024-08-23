// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Jobs.GitHubIndexer
{
    public static class Filters
    {
        public enum ConfigFileType
        {
            PackageReference,
            PackagesConfig,
            None
        }

        /// <summary>
        /// Returns the type of configuration file the filename points to. If the file type is not a valid config file,
        /// a ConfigFileType.NONE value is returned.
        /// </summary>
        /// <param name="file">The name or path of the file to look at</param>
        /// <exception cref="ArgumentNullException">Thrown then the filename is null</exception>
        /// <exception cref="ArgumentException">Thrown then the filename is invalid (Contains one or more chars defined in System.IO.Path.GetInvalidPathChars)</exception>
        /// <returns>An enum indicating the file type</returns>
        public static ConfigFileType GetConfigFileType(string file)
        {
            var fileName = Path.GetFileName(file);
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            if (string.Equals(fileName, "packages.config", StringComparison.OrdinalIgnoreCase))
            {
                return ConfigFileType.PackagesConfig;
            }

            var ext = Path.GetExtension(fileName);
            if (ext.EndsWith("proj", StringComparison.OrdinalIgnoreCase) 
                || ext.Equals(".props", StringComparison.OrdinalIgnoreCase) 
                || ext.Equals(".targets", StringComparison.OrdinalIgnoreCase))
            {
                return ConfigFileType.PackageReference;
            }

            return ConfigFileType.None;
        }
    }
}
