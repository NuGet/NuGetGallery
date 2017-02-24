// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet;
using Stats.AzureCdnLogs.Common;

namespace Stats.ImportAzureCdnStatistics
{
    public class PackageStatisticsParser
        : StatisticsParser, IPackageStatisticsParser
    {
        private readonly PackageTranslator _packageTranslator;

        public PackageStatisticsParser(PackageTranslator packageTranslator)
        {
            _packageTranslator = packageTranslator;
        }

        public PackageStatistics FromCdnLogEntry(CdnLogEntry cdnLogEntry)
        {
            var packageDefinition = PackageDefinition.FromRequestUrl(cdnLogEntry.RequestUrl);

            if (packageDefinition == null)
            {
                return null;
            }

            packageDefinition = _packageTranslator.TranslatePackageDefinition(packageDefinition);

            var statistic = new PackageStatistics();
            statistic.EdgeServerTimeDelivered = cdnLogEntry.EdgeServerTimeDelivered;
            statistic.PackageId = packageDefinition.PackageId;
            statistic.PackageVersion = NormalizeSemanticVersion(packageDefinition.PackageVersion);

            var customFieldDictionary = CdnLogCustomFieldParser.Parse(cdnLogEntry.CustomField);
            statistic.Operation = GetCustomFieldValue(customFieldDictionary, NuGetCustomHeaders.NuGetOperation);
            statistic.DependentPackage = GetCustomFieldValue(customFieldDictionary, NuGetCustomHeaders.NuGetDependentPackage);
            statistic.ProjectGuids = GetCustomFieldValue(customFieldDictionary, NuGetCustomHeaders.NuGetProjectGuids);
            statistic.UserAgent = GetUserAgentValue(cdnLogEntry);
            statistic.EdgeServerIpAddress = cdnLogEntry.EdgeServerIpAddress;

            // ignore blacklisted user agents
            if (!IsBlackListed(statistic.UserAgent))
            {
                return statistic;
            }
            return null;
        }

        private static string NormalizeSemanticVersion(string packageVersion)
        {
            // Normalize package version
            SemanticVersion semanticVersion;
            if (!string.IsNullOrEmpty(packageVersion)
                && SemanticVersion.TryParse(packageVersion, out semanticVersion))
            {
                packageVersion = semanticVersion.ToNormalizedString();
            }

            return packageVersion;
        }
    }
}