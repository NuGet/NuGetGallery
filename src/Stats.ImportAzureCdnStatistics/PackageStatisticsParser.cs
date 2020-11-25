// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using Stats.AzureCdnLogs.Common;
using Stats.LogInterpretation;

namespace Stats.ImportAzureCdnStatistics
{
    public class PackageStatisticsParser
        : StatisticsParser, IPackageStatisticsParser
    {
        private readonly ILogger<PackageStatisticsParser> _logger;
        private readonly PackageTranslator _packageTranslator;

        public PackageStatisticsParser(PackageTranslator packageTranslator, ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _packageTranslator = packageTranslator;
            _logger = loggerFactory.CreateLogger<PackageStatisticsParser>();
        }

        public PackageStatistics FromCdnLogEntry(CdnLogEntry cdnLogEntry)
        {
            var packageDefinitions = PackageDefinition.FromRequestUrl(cdnLogEntry.RequestUrl);

            if (packageDefinitions == null || !packageDefinitions.Any())
            {
                return null;
            }

            if (packageDefinitions.Count > 1)
            {
                _logger.LogWarning(LogEvents.MultiplePackageIDVersionParseOptions, 
                                   "Found multiple parse options for URL {RequestUrl}: {PackageDefinitions}",
                                   cdnLogEntry.RequestUrl,
                                   string.Join(", ", packageDefinitions));
            }

            var packageDefinition = packageDefinitions.First();

            if (_packageTranslator != null)
            {
                bool translateOccured = _packageTranslator.TryTranslatePackageDefinition(packageDefinition);

                if (translateOccured)
                {
                    _logger.LogInformation(LogEvents.TranslatedPackageIdVersion,
                                           "Translated package. Url: {RequestUrl}, New definition: {PackageDefinition}",
                                           cdnLogEntry.RequestUrl,
                                           packageDefinition);
                }
            }

            var statistic = new PackageStatistics();
            statistic.EdgeServerTimeDelivered = cdnLogEntry.EdgeServerTimeDelivered;
            statistic.PackageId = packageDefinition.PackageId;
            statistic.PackageVersion = NormalizeSemanticVersion(packageDefinition.PackageVersion);

            var customFieldDictionary = CdnLogCustomFieldParser.Parse(cdnLogEntry.CustomField);
            statistic.Operation = GetCustomFieldValue(customFieldDictionary, NuGetCustomHeaders.NuGetOperation);
            statistic.DependentPackage = GetCustomFieldValue(customFieldDictionary, NuGetCustomHeaders.NuGetDependentPackage);
            statistic.UserAgent = GetUserAgentValue(cdnLogEntry);
            statistic.EdgeServerIpAddress = cdnLogEntry.EdgeServerIpAddress;

            // Ignore blacklisted user agents
            if (!IsBlackListed(statistic.UserAgent))
            {
                return statistic;
            }
            return null;
        }

        private static string NormalizeSemanticVersion(string packageVersion)
        {
            // Normalize package version
            NuGetVersion semanticVersion;
            if (!string.IsNullOrEmpty(packageVersion)
                && NuGetVersion.TryParse(packageVersion, out semanticVersion))
            {
                packageVersion = semanticVersion.ToNormalizedString();
            }

            return packageVersion;
        }
    }
}