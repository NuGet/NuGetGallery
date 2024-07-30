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
        private const string _nugetExeUrlEnding = "/nuget.exe";
        private const string _nugetExeLatestVersionSegment = "latest";
        private const string _nugetExePackageId = "tool/nuget.exe"; // to eliminate the chances of clashing with a real package
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

        public static PackageDefinition FromNuGetExeUrl(string requestUrl)
        {
            if (string.IsNullOrWhiteSpace(requestUrl) || !requestUrl.EndsWith(_nugetExeUrlEnding, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            // path example: /artifacts/win-x86-commandline/v5.9.1/nuget.exe

            requestUrl = HttpUtility.UrlDecode(requestUrl);

            var urlSegments = requestUrl.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            if (urlSegments.Length < 4)
            {
                // proper nuget.exe URL paths have at least 4 segments
                return null;
            }

            var suspectedVersionSegment = urlSegments[urlSegments.Length - 2];

            if (suspectedVersionSegment == _nugetExeLatestVersionSegment)
            {
                return new PackageDefinition(_nugetExePackageId, _nugetExeLatestVersionSegment);
            }

            if (!suspectedVersionSegment.StartsWith("v"))
            {
                return null;
            }

            var versionString = suspectedVersionSegment.Substring(1);
            if (NuGetVersion.TryParse(versionString, out var parsedVersion))
            {
                return new PackageDefinition(_nugetExePackageId, parsedVersion.ToNormalizedString());
            }

            return null;
        }

        public override string ToString()
        {
            return $"[{PackageId}, {PackageVersion}]";
        }
    }
}