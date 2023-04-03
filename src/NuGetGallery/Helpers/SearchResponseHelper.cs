// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery.Helpers
{
    public class SearchResponseHelper
    {
        public static ICollection<PackageDeprecation> GetDeprecations(JToken docDeprecation)
        {
            PackageDeprecation deprecation = null;
            if (docDeprecation != null)
            {
                var docReasons = docDeprecation.Value<JArray>("Reasons");
                if (docReasons != null && docReasons.HasValues)
                {
                    PackageDeprecationStatus status = PackageDeprecationStatus.NotDeprecated;
                    foreach (var reason in docReasons)
                    {
                        if (Enum.TryParse<PackageDeprecationStatus>(reason.Value<string>(), out var pdStatus))
                        {
                            status |= pdStatus;
                        }
                    }

                    var docAlternatePackage = docDeprecation["AlternatePackage"];
                    Package alternatePackage = null;
                    if (docAlternatePackage != null)
                    {
                        var range = docAlternatePackage.Value<string>("Range");
                        var version = string.Empty;
                        if (range.StartsWith("["))
                        {
                            version = range.Substring(1, range.IndexOf(", )"));
                        }

                        alternatePackage = new Package()
                        {
                            Id = docAlternatePackage.Value<string>("Id"),
                            Version = version
                        };
                    }

                    deprecation = new PackageDeprecation()
                    {
                        CustomMessage = docDeprecation.Value<string>("Message"),
                        Status = status,
                        AlternatePackage = alternatePackage
                    };
                }
            }

            return deprecation == null ? null : new List<PackageDeprecation>() { deprecation };
        }

        public static ICollection<VulnerablePackageVersionRange> GetVulnerabilities(JArray docVulnerabilities)
        {
            var vulnerabilities = new List<VulnerablePackageVersionRange>();
            if (docVulnerabilities != null)
            {
                vulnerabilities = docVulnerabilities.Select(v => new VulnerablePackageVersionRange()
                {
                    Vulnerability = new PackageVulnerability()
                    {
                        AdvisoryUrl = v.Value<string>("AdvisoryURL"),
                        Severity = (PackageVulnerabilitySeverity)v.Value<int>("Severity")
                    }
                })
                .ToList();
            }

            return vulnerabilities;
        }
    }
}