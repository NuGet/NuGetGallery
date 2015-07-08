// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Stats.AzureCdnLogs.Common
{
    public class PackageDefinition
    {
        private const string _nupkgExtension = ".nupkg";

        public string PackageId { get; set; }
        public string PackageVersion { get; set; }

        public static PackageDefinition FromRequestUrl(string requestUrl)
        {
            var urlSegments = requestUrl.Split('/');
            var fileName = urlSegments.Last();

            if (fileName.EndsWith(_nupkgExtension))
            {
                var fileNameSegments = fileName.Substring(0, fileName.Length - _nupkgExtension.Length).Split('.');
                var packageIdSegments = new List<string>();
                var packageVersionSegments = new List<string>();

                int? firstPackageVersionSegment = null;
                for (var i = 0; i < fileNameSegments.Length; i++)
                {
                    var segment = fileNameSegments[i];
                    int parsedSegement;
                    var isNumericSegment = int.TryParse(segment, out parsedSegement);
                    if ((!isNumericSegment && !firstPackageVersionSegment.HasValue) || (!isNumericSegment && i < firstPackageVersionSegment.Value))
                    {
                        packageIdSegments.Add(segment);
                    }
                    else
                    {
                        if (!firstPackageVersionSegment.HasValue)
                        {
                            firstPackageVersionSegment = i;
                        }
                        packageVersionSegments.Add(segment);
                    }
                }

                var packageDefinition = new PackageDefinition();
                packageDefinition.PackageId = string.Join(".", packageIdSegments);
                packageDefinition.PackageVersion = string.Join(".", packageVersionSegments);

                return packageDefinition;
            }
            else return null;
        }
    }
}