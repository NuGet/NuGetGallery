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
        public static bool Verbose = false;

        public static int ChunkSize = 4000;

        public static List<Package> GetPublishedPackagesSince(string sqlConnectionString, int highestPackageKey)
        {
            EntitiesContext context = new EntitiesContext(sqlConnectionString, readOnly: true);
            IEntityRepository<Package> packageRepository = new EntityRepository<Package>(context);

            IQueryable<Package> set = packageRepository.GetAll();

            //  the query to get the id range is cheap and provides a workaround for EF limitations on Take() 

            Tuple<int, int> range = GetNextRange(sqlConnectionString, highestPackageKey, ChunkSize);

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

            set = set
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .Include(p => p.SupportedFrameworks);

            return ExecuteQuery(set);
        }

        public static List<Package> GetEditedPackagesSince(string sqlConnectionString, int highestPackageKey, DateTime lastEditsIndexTime)
        {
            EntitiesContext context = new EntitiesContext(sqlConnectionString, readOnly: true);
            IEntityRepository<Package> packageRepository = new EntityRepository<Package>(context);

            IQueryable<Package> set = packageRepository.GetAll();

            //  older edits can be reapplied so duplicates don't matter
            TimeSpan delta = TimeSpan.FromMinutes(2);
            DateTime startOfWindow = DateTime.MinValue;
            if (lastEditsIndexTime > DateTime.MinValue + delta)
            {
                startOfWindow = lastEditsIndexTime - delta;
            }

            //  we want to make sure we only apply edits for packages that we actually have in the index - to avoid a publish thread overwriting
            set = set.Where(p => p.LastEdited > startOfWindow && p.Key <= highestPackageKey);
            set = set.OrderBy(p => p.LastEdited);

            set = set
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .Include(p => p.SupportedFrameworks);

            return ExecuteQuery(set);
        }

        public static List<Package> ExecuteQuery(IQueryable<Package> query)
        {
            if (Verbose)
            {
                TraceWriter.WriteLine(query.ToString());
            }

            DateTime before = DateTime.Now;

            List<Package> list = query.ToList();

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

            if (Verbose)
            {
                TraceWriter.WriteLine(curatedFeedsPerPackageRegistrationGrouping.ToString());
            }

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
            //  this code only exists to work-around an EF limitation on using Take() with no Skip() but with additional constraints 

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

        public static Tuple<int, int, HashSet<int>> GetNextBlockOfPackageIds(string sqlConnectionString, int lastHighestPackageKey, int chunkSize)
        {
            string sql = @"
                SELECT TOP(@ChunkSize) Packages.[Key]
                FROM Packages
                WHERE Packages.[Key] > @LastHighestPackageKey
                ORDER BY Packages.[Key]
            ";

            using (SqlConnection connection = new SqlConnection(sqlConnectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("ChunkSize", chunkSize);
                command.Parameters.AddWithValue("LastHighestPackageKey", lastHighestPackageKey);

                SqlDataReader reader = command.ExecuteReader();

                int minPackageId = 0;
                int maxPackageId = 0;
                HashSet<int> packageIds = new HashSet<int>();

                bool firstIteration = true;

                while (reader.Read())
                {
                    int packageId = reader.GetInt32(0);

                    if (firstIteration)
                    {
                        firstIteration = false;
                        minPackageId = packageId;
                    }
                    else
                    {
                        if (packageId < minPackageId)
                        {
                            minPackageId = packageId;
                        }
                    }

                    if (packageId > maxPackageId)
                    {
                        maxPackageId = packageId;
                    }
                    
                    packageIds.Add(packageId);
                }

                return new Tuple<int, int, HashSet<int>>(minPackageId, maxPackageId, packageIds);
            }
        }
    }
}
