// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace NuGet.Jobs.GitHubIndexer
{
    public class ConfigFileParser : IConfigFileParser
    {
        private readonly RepoUtils _repoUtils;
        private readonly ILogger<ConfigFileParser> _logger;

        public ConfigFileParser(RepoUtils repoUtils, ILogger<ConfigFileParser> logger)
        {
            _repoUtils = repoUtils ?? throw new ArgumentNullException(nameof(repoUtils));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Parses a file and returns the NuGet dependencies stored in the file.
        /// If the file is an invalid config or project file, the call returns an empty list.
        /// </summary>
        /// <param name="file">File to parse</param>
        /// <returns>List of NuGet dependencies listed in the file</returns>
        public IReadOnlyList<string> Parse(ICheckedOutFile file)
        {
            var fileType = Filters.GetConfigFileType(file.Path);
            if (fileType == Filters.ConfigFileType.None)
            {
                return Array.Empty<string>();
            }

            _logger.LogInformation("[{RepoName}] Parsing file {FileName} !", file.RepoId, file.Path);
            using (var fileStream = file.OpenFile())
            {
                IReadOnlyList<string> res;
                switch (fileType)
                {
                    case Filters.ConfigFileType.PackagesConfig:
                        res = _repoUtils.ParsePackagesConfig(fileStream, file.RepoId);
                        break;
                    case Filters.ConfigFileType.PackageReference:
                        res = _repoUtils.ParseProjFile(fileStream, file.RepoId);
                        break;
                    default:
                        throw new ArgumentException($"Unhandled fileType {fileType}");
                }

                _logger.LogInformation("[{RepoName}] Found {Count} dependencies!", file.RepoId, res.Count);

                return res;
            }
        }
    }
}
