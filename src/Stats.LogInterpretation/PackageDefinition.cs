// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NuGet.Versioning;

namespace Stats.LogInterpretation
{
    public class PackageDefinition
    {
        private const string _nupkgExtension = ".nupkg";
        private const string _dotSeparator = ".";

        public string PackageId { get; set; }
        public string PackageVersion { get; set; }

        public PackageDefinition()
        {
        }

        private PackageDefinition(string packageId, string packageVersion)
        {
            PackageId = packageId.Trim();
            PackageVersion = packageVersion.Trim();
        }

        public static IList<PackageDefinition> FromRequestUrl(string requestUrl)
        {
            if (string.IsNullOrWhiteSpace(requestUrl) || !requestUrl.EndsWith(_nupkgExtension, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            List<PackageDefinition> resolutionOptions = new List<PackageDefinition>();

            requestUrl = HttpUtility.UrlDecode(requestUrl);

            var urlSegments = requestUrl.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
          
            var fileName = urlSegments.Last();
           
            fileName = fileName.Remove(fileName.Length - _nupkgExtension.Length, _nupkgExtension.Length);

            // Special handling for flat container
            if (urlSegments.Length > 3)
            {
                var packageIdContainer = urlSegments[urlSegments.Length - 3];
                var packageVersionContainer = urlSegments[urlSegments.Length - 2];

                if (string.Equals(fileName, $"{packageIdContainer}.{packageVersionContainer}", StringComparison.InvariantCultureIgnoreCase))
                {
                    resolutionOptions.Add(new PackageDefinition(packageIdContainer, packageVersionContainer));
                }
            }

            if (!resolutionOptions.Any())
            {
                var nextDotIndex = fileName.IndexOf('.');

                while (nextDotIndex != -1)
                {
                    string packagePart = fileName.Substring(0, nextDotIndex);
                    string versionPart = fileName.Substring(nextDotIndex + 1);

                    if (NuGetVersion.TryParse(versionPart, out var parsedVersion))
                    {
                        var normalizedVersion = parsedVersion.ToNormalizedString();

                        if (string.Equals(normalizedVersion, versionPart, StringComparison.InvariantCultureIgnoreCase))
                        {
                            resolutionOptions.Add(new PackageDefinition(packagePart, versionPart));
                        }
                    }

                    nextDotIndex = fileName.IndexOf('.', nextDotIndex + 1);
                }
            }

            return resolutionOptions;
        }

        public override string ToString()
        {
            return $"[{PackageId}, {PackageVersion}]";
        }
    }
}