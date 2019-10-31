// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;

namespace GitHubVulnerabilities2Db.Ingest
{
    public class GitHubVersionRangeParser : IGitHubVersionRangeParser
    {
        public VersionRange ToNuGetVersionRange(string gitHubVersionRange)
        {
            if (string.IsNullOrWhiteSpace(gitHubVersionRange))
            {
                throw new GitHubVersionRangeParsingException(
                    gitHubVersionRange, "A version range cannot be null or whitespace!");
            }

            // Remove commas in version range. They exist solely for readability.
            var gitHubVersionRangeWithoutCommas = gitHubVersionRange.Replace(",", string.Empty);

            // A GitHub version range consists of pairs of:
            // 1. A symbol (<, >, <=, or >=), which defines whether the next version is the minimum or maximum and whether or not it's included or excluded in the range.
            // 2. A SemVer version.
            var versionRangeParts = gitHubVersionRangeWithoutCommas.Split(' ');
            if (versionRangeParts.Length > 4)
            {
                throw new GitHubVersionRangeParsingException(
                    gitHubVersionRange, "A version range cannot contain more than two pairs.");
            }

            NuGetVersion minVersion = null;
            var includeMinVersion = false;
            NuGetVersion maxVersion = null;
            var includeMaxVersion = false;

            for (var i = 0; i < versionRangeParts.Length; i += 2)
            {
                if (versionRangeParts.Length <= i + 1)
                {
                    throw new GitHubVersionRangeParsingException(
                        gitHubVersionRange, "The number of version range parts must be a multiple of two.");
                }

                // The symbol is the first part of the version range pair.
                var symbol = versionRangeParts[i];

                // The version is the second part of the version range pair.
                var version = NuGetVersion.Parse(versionRangeParts[i + 1]);
                var isMin = false;
                var isMax = false;

                switch (symbol)
                {
                    case "=":
                        isMin = true;
                        includeMinVersion = true;
                        isMax = true;
                        includeMaxVersion = true;
                        break;
                    case "<":
                        isMax = true;
                        break;
                    case "<=":
                        isMax = true;
                        includeMaxVersion = true;
                        break;
                    case ">":
                        isMin = true;
                        break;
                    case ">=":
                        isMin = true;
                        includeMinVersion = true;
                        break;
                    default:
                        throw new GitHubVersionRangeParsingException(
                            gitHubVersionRange, $"{symbol} is not a valid symbol in a version range.");
                }

                if (isMin)
                {
                    if (minVersion == null)
                    {
                        minVersion = version;
                    }
                    else
                    {
                        throw new GitHubVersionRangeParsingException(
                            gitHubVersionRange, "The minimum version is already defined for the version range.");
                    }
                }

                if (isMax)
                {
                    if (maxVersion == null)
                    {
                        maxVersion = version;
                    }
                    else
                    {
                        throw new GitHubVersionRangeParsingException(
                            gitHubVersionRange, "The maximum version is already defined for the version range.");
                    }
                }
            }

            return new VersionRange(minVersion, includeMinVersion, maxVersion, includeMaxVersion);
        }
    }
}