// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data.SqlTypes;
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

        private readonly ISqlConnectionFactory _connectionFactory;
        private readonly Db2CatalogProjection _db2catalogProjection;
        private readonly int _commandTimeout;

        public GalleryDatabaseQueryService(
            ISqlConnectionFactory connectionFactory,
            PackageContentUriBuilder packageContentUriBuilder,
            int commandTimeout)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
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
                        PR.[Id],
                        P.[NormalizedVersion],
                        P.[Created],
                        P.[LastEdited],
                        P.[Published],
                        P.[Listed],
                        P.[HideLicenseReport],
                        P.[LicenseNames],
                        P.[LicenseReportUrl]
                    FROM [dbo].[Packages] AS P
                    INNER JOIN [dbo].[PackageRegistrations] AS PR ON P.[PackageRegistrationKey] = PR.[Key]
                    WHERE P.[PackageStatusKey] = {(int)PackageStatus.Available} 
                        AND P.[{cursor.ColumnName}] > @{CursorParameterName}
                    ORDER BY P.[{cursor.ColumnName}]";
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

                using (var packagesReader = await packagesCommand.ExecuteReaderAsync())
                {
                    while (await packagesReader.ReadAsync())
                    {
                        packages.Add(_db2catalogProjection.FromDataRecord(packagesReader));
                    }
                }
            }

            return packages;
        }

        public sealed class Db2CatalogCursor
        {
            public const string ColumnNameCreated = "Created";
            public const string ColumnNameLastEdited = "LastEdited";

            private Db2CatalogCursor(string columnName, DateTime cursorValue, int top)
            {
                ColumnName = columnName ?? throw new ArgumentNullException(nameof(columnName));
                CursorValue = cursorValue < SqlDateTime.MinValue.Value ? SqlDateTime.MinValue.Value : cursorValue;

                if (top <= 0)
                {
                    throw new ArgumentOutOfRangeException("Argument value must be a positive non-zero integer.", nameof(top));
                }

                Top = top;
            }

            public string ColumnName { get; }
            public DateTime CursorValue { get; }
            public int Top { get; }

            public static Db2CatalogCursor ByCreated(DateTime since, int top) => new Db2CatalogCursor(ColumnNameCreated, since, top);
            public static Db2CatalogCursor ByLastEdited(DateTime since, int top) => new Db2CatalogCursor(ColumnNameLastEdited, since, top);
        }
    }
}