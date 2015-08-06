// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Stats.AzureCdnLogs.Common
{
    public class PackageDefinition
    {
        private const string _nupkgExtension = ".nupkg";
        private const string _dotSeparator = ".";

        public string PackageId { get; set; }
        public string PackageVersion { get; set; }

        public static PackageDefinition FromRequestUrl(string requestUrl)
        {
            if (string.IsNullOrWhiteSpace(requestUrl) || !requestUrl.EndsWith(_nupkgExtension))
            {
                return null;
            }

            requestUrl = HttpUtility.UrlDecode(requestUrl);


            var urlSegments = requestUrl.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
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
                    if (i == 0)
                    {
                        // first segment is always part of package id
                        packageIdSegments.Add(segment);
                        continue;
                    }

                    if (i < fileNameSegments.Length - 4)
                    {
                        // version part can only contain 4 segments maximum
                        packageIdSegments.Add(segment);
                        continue;
                    }

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
                packageDefinition.PackageId = string.Join(_dotSeparator, packageIdSegments);
                packageDefinition.PackageVersion = string.Join(_dotSeparator, packageVersionSegments);

                return packageDefinition;
            }
            else return null;
        }
    }
}