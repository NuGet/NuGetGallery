// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    /// <summary>
    /// Utility class to project <see cref="IDataRecord"/>s retrieved by db2catalog
    /// into a format that is consumable by the catalog writer.
    /// </summary>
    public class Db2CatalogProjection
    {
        public const string AlternatePackageVersionWildCard = "*";

        private readonly PackageContentUriBuilder _packageContentUriBuilder;

        public Db2CatalogProjection(PackageContentUriBuilder packageContentUriBuilder)
        {
            _packageContentUriBuilder = packageContentUriBuilder ?? throw new ArgumentNullException(nameof(packageContentUriBuilder));
        }

        public FeedPackageDetails ReadFeedPackageDetailsFromDataReader(DbDataReader dataReader)
        {
            if (dataReader == null)
            {
                throw new ArgumentNullException(nameof(dataReader));
            }

            var packageId = dataReader[Db2CatalogProjectionColumnNames.PackageId].ToString();
            var normalizedPackageVersion = dataReader[Db2CatalogProjectionColumnNames.NormalizedVersion].ToString();
            var listed = dataReader.GetBoolean(dataReader.GetOrdinal(Db2CatalogProjectionColumnNames.Listed));
            var hideLicenseReport = dataReader.GetBoolean(dataReader.GetOrdinal(Db2CatalogProjectionColumnNames.HideLicenseReport));

            var packageContentUri = _packageContentUriBuilder.Build(packageId, normalizedPackageVersion);
            var deprecationInfo = ReadDeprecationInfoFromDataReader(dataReader);

            return new FeedPackageDetails(
                packageContentUri,
                dataReader.ReadDateTime(Db2CatalogProjectionColumnNames.Created).ForceUtc(),
                dataReader.ReadNullableUtcDateTime(Db2CatalogProjectionColumnNames.LastEdited),
                listed ? dataReader.ReadDateTime(Db2CatalogProjectionColumnNames.Published).ForceUtc() : Constants.UnpublishedDate,
                packageId,
                normalizedPackageVersion,
                hideLicenseReport ? null : dataReader[Db2CatalogProjectionColumnNames.LicenseNames]?.ToString(),
                hideLicenseReport ? null : dataReader[Db2CatalogProjectionColumnNames.LicenseReportUrl]?.ToString(),
                deprecationInfo);
        }

        public PackageDeprecationItem ReadDeprecationInfoFromDataReader(DbDataReader dataReader)
        {
            if (dataReader == null)
            {
                throw new ArgumentNullException(nameof(dataReader));
            }

            var deprecationReasons = new List<string>();
            var deprecationStatusValue = dataReader.ReadInt32OrNull(Db2CatalogProjectionColumnNames.DeprecationStatus);

            if (!deprecationStatusValue.HasValue)
            {
                return null;
            }

            var deprecationStatus = (PackageDeprecationStatus)deprecationStatusValue.Value;

            foreach (var deprecationStatusFlag in Enum.GetValues(typeof(PackageDeprecationStatus)).Cast<PackageDeprecationStatus>())
            {
                if (deprecationStatusFlag == PackageDeprecationStatus.NotDeprecated)
                {
                    continue;
                }

                if (deprecationStatus.HasFlag(deprecationStatusFlag))
                {
                    deprecationReasons.Add(deprecationStatusFlag.ToString());
                }
            }

            var alternatePackageId = dataReader.ReadStringOrNull(Db2CatalogProjectionColumnNames.AlternatePackageId);
            string alternatePackageVersion = null;
            if (alternatePackageId != null)
            {
                alternatePackageVersion = dataReader.ReadStringOrNull(Db2CatalogProjectionColumnNames.AlternatePackageVersion);

                if (alternatePackageVersion == null)
                {
                    alternatePackageVersion = AlternatePackageVersionWildCard;
                }
                else
                {
                    alternatePackageVersion = $"[{alternatePackageVersion}, {alternatePackageVersion}]";
                }
            }

            return new PackageDeprecationItem(
                deprecationReasons,
                dataReader.ReadStringOrNull(Db2CatalogProjectionColumnNames.DeprecationMessage),
                alternatePackageId,
                alternatePackageVersion);
        }
    }
}