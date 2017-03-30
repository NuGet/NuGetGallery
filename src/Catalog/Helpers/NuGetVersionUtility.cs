// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;
using VDS.RDF;
using VDS.RDF.Query;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public static class NuGetVersionUtility
    {
        public static string NormalizeVersion(string version)
        {
            NuGetVersion parsedVersion;
            if (!NuGetVersion.TryParse(version, out parsedVersion))
            {
                return version;
            }

            return parsedVersion.ToNormalizedString();
        }

        public static string NormalizeVersionRange(string versionRange)
        {
            VersionRange parsedVersionRange;
            if (!VersionRange.TryParse(versionRange, out parsedVersionRange))
            {
                return versionRange;
            }

            return parsedVersionRange.ToNormalizedString();
        }

        public static string GetFullVersionString(string version)
        {
            NuGetVersion parsedVersion;
            if (!NuGetVersion.TryParse(version, out parsedVersion))
            {
                return version;
            }

            return parsedVersion.ToFullString();
        }

        public static bool IsVersionSemVer2(string version)
        {
            NuGetVersion parsedVersion;
            if (!NuGetVersion.TryParse(version, out parsedVersion))
            {
                return false;
            }

            return parsedVersion.IsSemVer2;
        }

        public static bool IsVersionRangeSemVer2(string versionRange)
        {
            VersionRange parsedVersionRange;
            if (!VersionRange.TryParse(versionRange, out parsedVersionRange))
            {
                return false;
            }

            if (parsedVersionRange.HasLowerBound && parsedVersionRange.MinVersion.IsSemVer2)
            {
                return true;
            }

            if (parsedVersionRange.HasUpperBound && parsedVersionRange.MaxVersion.IsSemVer2)
            {
                return true;
            }

            return false;
        }

        public static bool IsGraphSemVer2(string version, string resourceUri, IGraph graph)
        {
            // Is the package version itself SemVer 2.0.0?
            if (IsVersionSemVer2(version))
            {
                return true;
            }

            if (resourceUri == null)
            {
                throw new ArgumentNullException(nameof(resourceUri));
            }

            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            // Are any of the dependency version ranges SemVer 2.0.0?
            var sparql = new SparqlParameterizedString
            {
                CommandText = Utils.GetResource("sparql.SelectDistinctDependencyVersionRanges.rq")
            };
            sparql.SetUri("resourceUri", new Uri(resourceUri));
            var query = sparql.ToString();

            TripleStore store = new TripleStore();
            store.Add(graph, true);
            foreach (SparqlResult row in SparqlHelpers.Select(store, query))
            {
                var unparsedVersionRange = row["versionRange"].ToString();
                if (IsVersionRangeSemVer2(unparsedVersionRange))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
