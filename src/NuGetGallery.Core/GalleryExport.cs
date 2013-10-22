using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.IO;
using System.Linq;

namespace NuGetGallery
{
    public static class GalleryExport
    {
        public static TextWriter TraceWriter = Console.Out;

        public static int ChunkSize = 4000;

        public static List<Package> GetPublishedPackagesSince(string sqlConnectionString, DateTime indexTime, int highestPackageKey)
        {
            return GetPackages(sqlConnectionString, indexTime, highestPackageKey, null);
        }

        public static List<Package> GetEditedPackagesSince(string sqlConnectionString, DateTime indexTime, DateTime lastIndexTime)
        {
            return GetPackages(sqlConnectionString, indexTime, null, lastIndexTime);
        }

        public static List<Package> GetAllPackages(string sqlConnectionString, DateTime indexTime)
        {
            return GetPackages(sqlConnectionString, indexTime, null, null);
        }

        public static List<Package> GetPackages(string sqlConnectionString, DateTime indexTime, int? highestPackageKey, DateTime? mostRecentEdited)
        {
            EntitiesContext context = new EntitiesContext(sqlConnectionString, readOnly: true);
            IEntityRepository<Package> packageSource = new EntityRepository<Package>(context);

            IQueryable<Package> set = packageSource.GetAll();

            if (highestPackageKey.HasValue)
            {
                Tuple<int, int> range = GetNextRange(sqlConnectionString, highestPackageKey.Value, ChunkSize);

                if (range.Item1 == 0 && range.Item2 == 0)
                {
                    //  make sure Key == 0 returns no rows and so we quit
                    set = set.Where(p => 1 == 2);
                }
                else
                {
                    set = set.Where(p => p.Key >= range.Item1 && p.Key <= range.Item2);
                    set = set.OrderBy(p => p.Key);
                }
            }
            else if (mostRecentEdited.HasValue)
            {
                set = set.Where(p => p.LastEdited >= mostRecentEdited.Value);
                set = set.OrderBy(p => p.LastEdited);
            }

            set = set
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .Include(p => p.SupportedFrameworks);

            //  always set an upper bound on the set we get as this is what we will timestamp the index with
            //set = set.Where(p => p.LastUpdated < indexTime);

            TraceWriter.WriteLine(EntityFrameworkTracing.ToTraceString(set));

            //  database call

            DateTime before = DateTime.Now;

            List<Package> list = set.ToList();

            DateTime after = DateTime.Now;

            TraceWriter.WriteLine("Packages: {0} rows returned, duration {1} seconds", list.Count, (after - before).TotalSeconds);

            return list;
        }

        public static IDictionary<int, IEnumerable<string>>  GetFeedsByPackageRegistration(string sqlConnectionString)
        {
            EntitiesContext context = new EntitiesContext(sqlConnectionString, readOnly: true);
            IEntityRepository<CuratedPackage> curatedPackageRepository = new EntityRepository<CuratedPackage>(context);

            var curatedFeedsPerPackageRegistrationGrouping = curatedPackageRepository.GetAll()
                .Include(c => c.CuratedFeed)
                .Select(cp => new { PackageRegistrationKey = cp.PackageRegistrationKey, FeedName = cp.CuratedFeed.Name })
                .GroupBy(x => x.PackageRegistrationKey);

            TraceWriter.WriteLine(EntityFrameworkTracing.ToTraceString(curatedFeedsPerPackageRegistrationGrouping));

            //  database call

            DateTime before = DateTime.Now;

            IDictionary<int, IEnumerable<string>> feeds = curatedFeedsPerPackageRegistrationGrouping
                .ToDictionary(group => group.Key, element => element.Select(x => x.FeedName));

            DateTime after = DateTime.Now;

            TraceWriter.WriteLine("Feeds: {0} rows returned, duration {1} seconds", feeds.Count, (after - before).TotalSeconds);

            return feeds;
        }

        private static Tuple<int, int> GetNextRange(string sqlConnectionString, int lastHighestPackageKey, int chunkSize)
        {
            string sql = @"
                SELECT ISNULL(MIN(A.[Key]), 0), ISNULL(MAX(A.[Key]), 0)
                FROM (
                    SELECT TOP(@ChunkSize) Packages.[Key]
                    FROM Packages
                    INNER JOIN PackageRegistrations ON Packages.[PackageRegistrationKey] = PackageRegistrations.[Key]
                    WHERE Packages.[Key] > @LastHighestPackageKey
                    ORDER BY Packages.[Key]) AS A
            ";

            using (SqlConnection connection = new SqlConnection(sqlConnectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("ChunkSize", chunkSize);
                command.Parameters.AddWithValue("LastHighestPackageKey", lastHighestPackageKey);

                SqlDataReader reader = command.ExecuteReader();

                int min = 0;
                int max = 0;

                while (reader.Read())
                {
                    min = reader.GetInt32(0);
                    max = reader.GetInt32(1);
                }

                return new Tuple<int, int>(min, max);
            }
        }
    }
}
