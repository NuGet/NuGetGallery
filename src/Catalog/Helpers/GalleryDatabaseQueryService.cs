// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGet.Services.Sql;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    /// <summary>
    /// Utility class for all SQL queries invoked by Db2Catalog.
    /// </summary>
    public class GalleryDatabaseQueryService : IGalleryDatabaseQueryService
    {
        private const string CursorParameterName = "Cursor";
        private const string PackageIdParameterName = "PackageId";
        private const string PackageVersionParameterName = "PackageVersion";

        private static readonly string Db2CatalogSqlSubQuery = $@" PR.[Id],	
                        P.[NormalizedVersion],	
                        P.[Created],	
                        P.[LastEdited],
                        P.[Published],
                        P.[Listed],
                        P.[HideLicenseReport],
                        P.[LicenseNames],
                        P.[LicenseReportUrl],
                        PD.[Status] AS '{Db2CatalogProjectionColumnNames.DeprecationStatus}',
                        APR.[Id] AS '{Db2CatalogProjectionColumnNames.AlternatePackageId}',
                        AP.[NormalizedVersion] AS '{Db2CatalogProjectionColumnNames.AlternatePackageVersion}',
                        PD.[CustomMessage] AS '{Db2CatalogProjectionColumnNames.DeprecationMessage}'
                    FROM [dbo].[Packages] AS P
                    INNER JOIN [dbo].[PackageRegistrations] AS PR ON P.[PackageRegistrationKey] = PR.[Key]
                    LEFT JOIN [dbo].[PackageDeprecations] AS PD ON PD.[PackageKey] = P.[Key]
                    LEFT JOIN [dbo].[Packages] AS AP ON AP.[Key] = PD.[AlternatePackageKey]
                    LEFT JOIN [dbo].[PackageRegistrations] AS APR ON APR.[Key] = ISNULL(AP.[PackageRegistrationKey], PD.[AlternatePackageRegistrationKey])
                    WHERE P.[PackageStatusKey] = {(int)PackageStatus.Available} ";

        private readonly ISqlConnectionFactory _connectionFactory;
        private readonly Db2CatalogProjection _db2catalogProjection;
        private readonly ITelemetryService _telemetryService;
        private readonly int _commandTimeout;

        public GalleryDatabaseQueryService(
            ISqlConnectionFactory connectionFactory,
            PackageContentUriBuilder packageContentUriBuilder,
            ITelemetryService telemetryService,
            int commandTimeout)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _db2catalogProjection = new Db2CatalogProjection(packageContentUriBuilder);
            _commandTimeout = commandTimeout;
        }

        public Task<SortedList<DateTime, IList<FeedPackageDetails>>> GetPackagesCreatedSince(DateTime since, int top)
        {
            return GetPackagesInOrder(
                package => package.CreatedDate,
                Db2CatalogCursor.ByCreated(since, top));
        }

        public Task<SortedList<DateTime, IList<FeedPackageDetails>>> GetPackagesEditedSince(DateTime since, int top)
        {
            return GetPackagesInOrder(
                package => package.LastEditedDate,
                Db2CatalogCursor.ByLastEdited(since, top));
        }

        public async Task<FeedPackageDetails> GetPackageOrNull(string id, string version)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            var packages = new List<FeedPackageDetails>();
            var packageQuery = BuildGetPackageSqlQuery();

            using (var sqlConnection = await _connectionFactory.OpenAsync())
            {
                using (var packagesCommand = new SqlCommand(packageQuery, sqlConnection)
                {
                    CommandTimeout = _commandTimeout
                })
                {
                    packagesCommand.Parameters.AddWithValue(PackageIdParameterName, id);
                    packagesCommand.Parameters.AddWithValue(PackageVersionParameterName, version);

                    using (_telemetryService.TrackGetPackageQueryDuration(id, version))
                    {
                        using (var packagesReader = await packagesCommand.ExecuteReaderAsync())
                        {
                            while (await packagesReader.ReadAsync())
                            {
                                packages.Add(_db2catalogProjection.ReadFeedPackageDetailsFromDataReader(packagesReader));
                            }
                        }
                    }
                }
            }

            return packages.SingleOrDefault();
        }

        /// <summary>
        /// Returns a <see cref="SortedList{DateTime, IList{FeedPackageDetails}}"/> from the gallery database.
        /// </summary>
        /// <param name="keyDateFunc">The <see cref="DateTime"/> field to sort the <see cref="FeedPackageDetails"/> on.</param>
        private async Task<SortedList<DateTime, IList<FeedPackageDetails>>> GetPackagesInOrder(
            Func<FeedPackageDetails, DateTime> keyDateFunc,
            Db2CatalogCursor cursor)
        {
            var allPackages = await GetPackages(cursor);

            return OrderPackagesByKeyDate(allPackages, keyDateFunc);
        }

        /// <summary>
        /// Returns a <see cref="SortedList{DateTime, IList{FeedPackageDetails}}"/> of packages.
        /// </summary>
        /// <param name="keyDateFunc">The <see cref="DateTime"/> field to sort the <see cref="FeedPackageDetails"/> on.</param>
        internal static SortedList<DateTime, IList<FeedPackageDetails>> OrderPackagesByKeyDate(
            IReadOnlyCollection<FeedPackageDetails> packages,
            Func<FeedPackageDetails, DateTime> keyDateFunc)
        {
            var result = new SortedList<DateTime, IList<FeedPackageDetails>>();

            foreach (var package in packages)
            {
                var packageKeyDate = keyDateFunc(package);
                if (!result.TryGetValue(packageKeyDate, out IList<FeedPackageDetails> packagesWithSameKeyDate))
                {
                    packagesWithSameKeyDate = new List<FeedPackageDetails>();
                    result.Add(packageKeyDate, packagesWithSameKeyDate);
                }

                packagesWithSameKeyDate.Add(package);
            }

            var packagesCount = 0;
            var filteredResult = new SortedList<DateTime, IList<FeedPackageDetails>>();
            foreach (var keyDate in result.Keys)
            {
                if (result.TryGetValue(keyDate, out IList<FeedPackageDetails> packagesForKeyDate))
                {
                    if (packagesCount > 0 && packagesCount + packagesForKeyDate.Count > Constants.MaxPageSize)
                    {
                        break;
                    }

                    packagesCount += packagesForKeyDate.Count;
                    filteredResult.Add(keyDate, packagesForKeyDate);
                }
            }

            return filteredResult;
        }

        /// <summary>
        /// Builds the SQL query string for db2catalog.
        /// </summary>
        /// <param name="cursor">The <see cref="Db2CatalogCursor"/> to be used.</param>
        /// <returns>The SQL query string for the db2catalog job, build from the the provided <see cref="Db2CatalogCursor"/>.</returns>
        internal static string BuildDb2CatalogSqlQuery(Db2CatalogCursor cursor)
        {
            return $@"SELECT TOP {cursor.Top} WITH TIES
                        {Db2CatalogSqlSubQuery}
                        AND P.[{cursor.ColumnName}] > @{CursorParameterName}
                    ORDER BY P.[{cursor.ColumnName}]";
        }

        /// <summary>
        /// Builds the parameterized SQL query for retrieving package details given an id and version.
        /// </summary>
        internal static string BuildGetPackageSqlQuery()
        {
            return $@"SELECT 
                        {Db2CatalogSqlSubQuery}
                        AND PR.[Id] = @PackageId
                        AND P.[NormalizedVersion] = @PackageVersion";
        }

        /// <summary>
        /// Asynchronously gets a <see cref="IReadOnlyCollection{FeedPackageDetails}"/> from the gallery database.
        /// </summary>
        /// <param name="cursor">Defines the cursor to be used.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns an
        /// <see cref="IReadOnlyCollection{FeedPackageDetails}" />.</returns>
        private async Task<IReadOnlyCollection<FeedPackageDetails>> GetPackages(Db2CatalogCursor cursor)
        {
            using (var sqlConnection = await _connectionFactory.OpenAsync())
            {
                return await GetPackageDetailsAsync(sqlConnection, cursor);
            }
        }

        private async Task<IReadOnlyCollection<FeedPackageDetails>> GetPackageDetailsAsync(
            SqlConnection sqlConnection,
            Db2CatalogCursor cursor)
        {
            var packages = new List<FeedPackageDetails>();
            var packageQuery = BuildDb2CatalogSqlQuery(cursor);

            using (var packagesCommand = new SqlCommand(packageQuery, sqlConnection)
            {
                CommandTimeout = _commandTimeout
            })
            {
                packagesCommand.Parameters.AddWithValue(CursorParameterName, cursor.CursorValue);

                using (_telemetryService.TrackGetPackageDetailsQueryDuration(cursor))
                {
                    using (var packagesReader = await packagesCommand.ExecuteReaderAsync())
                    {
                        while (await packagesReader.ReadAsync())
                        {
                            packages.Add(_db2catalogProjection.ReadFeedPackageDetailsFromDataReader(packagesReader));
                        }
                    }
                }
            }

            return packages;
        }
    }
}