// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Jobs.GitHubIndexer
{
    public interface IConfigFileParser
    {
        /// <summary>
        /// Parses a file and returns the NuGet dependencies stored in the file.
        /// </summary>
        /// <param name="file">File to parse</param>
        /// <returns>List of NuGet dependencies listed in the file</returns>
        IReadOnlyList<string> Parse(ICheckedOutFile file);
    }
}
