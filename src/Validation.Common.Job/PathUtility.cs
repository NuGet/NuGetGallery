// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Jobs.Validation
{
    public static class PathUtility
    {
        /// <summary>
        /// Checks if given file path is an absolute path
        /// </summary>
        public static bool IsFilePathAbsolute(string path)
        { 
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            return Path.IsPathRooted(path)
                    && Path.GetPathRoot(path).Length == 3
                    && Path.GetPathRoot(path).Substring(1) == ":" + Path.DirectorySeparatorChar;
        }
    }
}
