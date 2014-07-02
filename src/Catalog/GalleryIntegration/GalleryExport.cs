using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Data.SqlTypes;
using System.Security.Cryptography;
using System.Text;

namespace NuGet.Services.Metadata.Catalog.GalleryIntegration
{
    public class GalleryExport
    {
        private static HashAlgorithm _hash = new SHA1Managed();

        private const string CollectChecksumsSql = @"
            WITH cte AS (
                SELECT
                    p.[Key],
                    (
                        SELECT cf.Name + ',' 
                        FROM CuratedPackages cp
                        INNER JOIN CuratedFeeds cf ON cp.CuratedFeedKey = cf.[Key]
                        WHERE cp.PackageRegistrationKey = p.PackageRegistrationKey
                        FOR XML PATH('')
                    ) AS [Feeds],
                    p.[LastEdited], 
                    p.[Published], 
                    p.[Listed], 
                    p.[IsLatestStable], 
                    p.[IsLatest]
                FROM Packages p
            )
            SELECT TOP(@ChunkSize)
                *
            FROM cte
            WHERE [Key] > @LastHighestPackageKey
            ORDER BY [Key]";

        private const string CollectPackageDataSql = @"
            WITH cte AS (
                SELECT
                    p.*,
                    (
                        SELECT cf.Name + ',' 
                        FROM CuratedPackages cp
                        INNER JOIN CuratedFeeds cf ON cp.CuratedFeedKey = cf.[Key]
                        WHERE cp.PackageRegistrationKey = p.PackageRegistrationKey
                        FOR XML PATH('')
                    ) AS [Feeds]
                FROM Packages p
            )
            SELECT *
            FROM cte
            WHERE [Key] >= @MinKey
            AND [Key] <= @MaxKey
            ORDER BY [Key]";

        private const string FetchPackageKeyRangeSql = @"
            SELECT ISNULL(MIN(A.[Key]), 0), ISNULL(MAX(A.[Key]), 0)
            FROM (
                SELECT TOP(@ChunkSize) Packages.[Key]
                FROM Packages
                INNER JOIN PackageRegistrations ON Packages.[PackageRegistrationKey] = PackageRegistrations.[Key]
                WHERE Packages.[Key] > @LastHighestPackageKey
                ORDER BY Packages.[Key]) AS A";

        private const string FetchPackageRegistrationsSql = @"
            SELECT Packages.[Key] 'Key', PackageRegistrations.[Id] 'Id'
            FROM PackageRegistrations 
            INNER JOIN Packages ON PackageRegistrations.[Key] = Packages.[PackageRegistrationKey]
            WHERE Packages.[Key] >= @MinKey AND Packages.[Key] <= @MaxKey";

        private const string FetchPackageDependenciesSql = @"
            SELECT
                Packages.[Key] 'Key',
                PackageDependencies.[Id] 'Id',
                PackageDependencies.VersionSpec 'VersionSpec',
                ISNULL(PackageDependencies.TargetFramework, '') 'TargetFramework'
            FROM PackageDependencies
            INNER JOIN Packages ON PackageDependencies.[PackageKey] = Packages.[Key]
            WHERE Packages.[Key] >= @MinKey AND Packages.[Key] <= @MaxKey";

        private const string FetchPackageFrameworksSql = @"
            SELECT
                Packages.[Key] 'Key',
                PackageFrameworks.TargetFramework 'TargetFramework'
            FROM PackageFrameworks
            INNER JOIN Packages ON PackageFrameworks.[Package_Key] = Packages.[Key]
            WHERE Packages.[Key] >= @MinKey AND Packages.[Key] <= @MaxKey";

        public static IEnumerable<ChecksumComparisonResult> CompareChecksums(IDictionary<int, JObject> catalogChecksums, IDictionary<int, string> databaseChecksums)
        {
            foreach (var catalogPair in catalogChecksums)
            {
                string databaseChecksum;
                if (!databaseChecksums.TryGetValue(catalogPair.Key, out databaseChecksum))
                {
                    yield return new ChecksumComparisonResult(
                        catalogPair.Key,
                        catalogPair.Value.Value<string>("id"),
                        catalogPair.Value.Value<string>("version"),
                        ComparisonResult.PresentInCatalogOnly);
                }
                else
                {
                    databaseChecksums.Remove(catalogPair.Key);
                    if (!String.Equals(databaseChecksum, catalogPair.Value.Value<string>("checksum"), StringComparison.Ordinal))
                    {
                        yield return new ChecksumComparisonResult(
                            catalogPair.Key,
                            catalogPair.Value.Value<string>("id"),
                            catalogPair.Value.Value<string>("version"),
                            ComparisonResult.DifferentInCatalog);
                    }
                }
            }

            // Keys that remain in the DB section are new to the catalog
            foreach (var databasePair in databaseChecksums)
            {
                yield return new ChecksumComparisonResult(
                    databasePair.Key,
                    null,
                    null,
                    ComparisonResult.PresentInDatabaseOnly);
            }
        }

        public static async Task<Tuple<int, int>> GetNextRange(string sqlConnectionString, int lastHighestPackageKey, int chunkSize)
        {
            using (SqlConnection connection = new SqlConnection(sqlConnectionString))
            {
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand(FetchPackageKeyRangeSql, connection);
                command.Parameters.AddWithValue("ChunkSize", chunkSize);
                command.Parameters.AddWithValue("LastHighestPackageKey", lastHighestPackageKey);

                SqlDataReader reader = await command.ExecuteReaderAsync();

                int min = 0;
                int max = 0;

                while (await reader.ReadAsync())
                {
                    min = reader.GetInt32(0);
                    max = reader.GetInt32(1);
                }

                return new Tuple<int, int>(min, max);
            }
        }

        public static async Task WriteRange(string sqlConnectionString, Tuple<int, int> range, GalleryExportBatcher batcher)
        {
            // Be cautious if you're going to make these simultaneous, we don't want to overload the SQL Server.
            IDictionary<int, JObject> packages = await FetchPackages(sqlConnectionString, range);
            IDictionary<int, string> registrations = await FetchPackageRegistrations(sqlConnectionString, range);
            IDictionary<int, List<JObject>> dependencies = await FetchPackageDependencies(sqlConnectionString, range);
            IDictionary<int, List<string>> targetFrameworks = await FetchPackageFrameworks(sqlConnectionString, range);

            foreach (int key in packages.Keys)
            {
                string registration = null;
                if (!registrations.TryGetValue(key, out registration))
                {
                    Console.WriteLine("could not find registration for {0}", key);
                    continue;
                }

                List<JObject> dependency = null;
                dependencies.TryGetValue(key, out dependency);

                List<string> targetFramework = null;
                targetFrameworks.TryGetValue(key, out targetFramework);

                await batcher.Process(packages[key], registration, dependency, targetFramework);
            }
        }

        public static Task WritePackage(string sqlConnectionString, int key, GalleryExportBatcher batcher)
        {
            return WriteRange(sqlConnectionString, Tuple.Create(key, key), batcher);
        }

        public static async Task<JObject> FetchPackage(string sqlConnectionString, int key)
        {
            return (await FetchPackages(sqlConnectionString, Tuple.Create(key, key)))[key];
        }

        public static async Task<IDictionary<int, JObject>> FetchPackages(string sqlConnectionString, Tuple<int, int> range)
        {
            using (SqlConnection connection = new SqlConnection(sqlConnectionString))
            {
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand(CollectPackageDataSql, connection);
                command.Parameters.AddWithValue("MinKey", range.Item1);
                command.Parameters.AddWithValue("MaxKey", range.Item2);

                SqlDataReader reader = await command.ExecuteReaderAsync();

                IDictionary<int, JObject> packages = new Dictionary<int, JObject>();

                while (await reader.ReadAsync())
                {
                    int key = reader.GetInt32(reader.GetOrdinal("Key"));

                    JObject obj = new JObject();
                    for (int i = 0;
                    i < reader.FieldCount;
                    i++)
                    {
                        obj.Add(reader.GetName(i), new JValue(reader.GetValue(i)));
                    }

                    // Fill in the checksum
                    string checksumString = await CalculateChecksum(reader);
                    obj.Add("DatabaseChecksum", checksumString);

                    packages.Add(key, obj);
                }

                return packages;
            }
        }

        public static async Task<IDictionary<int, string>> FetchChecksums(string sqlConnectionString, int lastHighestPackageKey, int chunkSize)
        {
            using (SqlConnection connection = new SqlConnection(sqlConnectionString))
            {
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand(CollectChecksumsSql, connection);
                command.Parameters.AddWithValue("ChunkSize", chunkSize);
                command.Parameters.AddWithValue("LastHighestPackageKey", lastHighestPackageKey);

                SqlDataReader reader = await command.ExecuteReaderAsync();

                var dict = new Dictionary<int, string>(chunkSize);
                while (await reader.ReadAsync())
                {
                    int key = reader.GetInt32(reader.GetOrdinal("Key"));
                    string checksumString = await CalculateChecksum(reader);
                    dict[key] = checksumString;
                }
                return dict;
            }
        }

        public static async Task<IDictionary<int, string>> FetchPackageRegistrations(string sqlConnectionString, Tuple<int, int> range)
        {
            using (SqlConnection connection = new SqlConnection(sqlConnectionString))
            {
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand(FetchPackageRegistrationsSql, connection);
                command.Parameters.AddWithValue("MinKey", range.Item1);
                command.Parameters.AddWithValue("MaxKey", range.Item2);

                SqlDataReader reader = await command.ExecuteReaderAsync();

                IDictionary<int, string> registrations = new Dictionary<int, string>();

                while (await reader.ReadAsync())
                {
                    int key = reader.GetInt32(reader.GetOrdinal("Key"));
                    string id = reader.GetString(reader.GetOrdinal("Id"));

                    registrations.Add(key, id);
                }

                return registrations;
            }
        }

        public static async Task<IDictionary<int, List<JObject>>> FetchPackageDependencies(string sqlConnectionString, Tuple<int, int> range)
        {
            using (SqlConnection connection = new SqlConnection(sqlConnectionString))
            {
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand(FetchPackageDependenciesSql, connection);
                command.Parameters.AddWithValue("MinKey", range.Item1);
                command.Parameters.AddWithValue("MaxKey", range.Item2);

                SqlDataReader reader = await command.ExecuteReaderAsync();

                IDictionary<int, List<JObject>> dependencies = new Dictionary<int, List<JObject>>();

                while (await reader.ReadAsync())
                {
                    int key = reader.GetInt32(reader.GetOrdinal("Key"));

                    JObject obj = new JObject();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        obj.Add(reader.GetName(i), new JValue(reader.GetValue(i)));
                    }

                    List<JObject> value;
                    if (!dependencies.TryGetValue(key, out value))
                    {
                        value = new List<JObject>();
                        dependencies.Add(key, value);
                    }

                    value.Add(obj);
                }

                return dependencies;
            }
        }

        public static async Task<IDictionary<int, List<string>>> FetchPackageFrameworks(string sqlConnectionString, Tuple<int, int> range)
        {
            using (SqlConnection connection = new SqlConnection(sqlConnectionString))
            {
                await connection.OpenAsync();

                SqlCommand command = new SqlCommand(FetchPackageFrameworksSql, connection);
                command.Parameters.AddWithValue("MinKey", range.Item1);
                command.Parameters.AddWithValue("MaxKey", range.Item2);

                SqlDataReader reader = await command.ExecuteReaderAsync();

                IDictionary<int, List<string>> targetFrameworks = new Dictionary<int, List<string>>();

                while (await reader.ReadAsync())
                {
                    int key = reader.GetInt32(reader.GetOrdinal("Key"));
                    string targetFramework = reader.GetString(reader.GetOrdinal("TargetFramework"));

                    List<string> value;
                    if (!targetFrameworks.TryGetValue(key, out value))
                    {
                        value = new List<string>();
                        targetFrameworks.Add(key, value);
                    }

                    value.Add(targetFramework);
                }

                return targetFrameworks;
            }
        }

        private static async Task<string> CalculateChecksum(SqlDataReader reader)
        {
            DateTime? lastEdited = null;
            int lastEditedOrdinal = reader.GetOrdinal("LastEdited");
            if (!await reader.IsDBNullAsync(lastEditedOrdinal))
            {
                lastEdited = reader.GetDateTime(lastEditedOrdinal);
            }
            DateTime published = reader.GetDateTime(reader.GetOrdinal("Published"));
            bool listed = reader.GetBoolean(reader.GetOrdinal("Listed"));
            bool latestStable = reader.GetBoolean(reader.GetOrdinal("IsLatestStable"));
            bool latest = reader.GetBoolean(reader.GetOrdinal("IsLatest"));
            string feeds = reader.GetString(reader.GetOrdinal("Feeds"));
            string checksumInput = String.Concat(
                lastEdited == null ? String.Empty : lastEdited.Value.ToString("O"), "|",
                published.ToString("O"), "|",
                listed.ToString(), "|",
                latestStable.ToString(), "|",
                latest.ToString(), "|",
                feeds);
            string checksumString =
                Convert.ToBase64String(
                    _hash.ComputeHash(
                        Encoding.UTF8.GetBytes(checksumInput)));
            return checksumString;
        }
    }

    public class ChecksumComparisonResult
    {
        public int Key { get; private set; }
        public ComparisonResult Result { get; private set; }
        public string Id { get; private set; }
        public string Version { get; private set; }

        public ChecksumComparisonResult(int key, string id, string version, ComparisonResult result)
        {
            Key = key;
            Result = result;
            Id = id;
            Version = version;
        }
    }

    public enum ComparisonResult {
        PresentInCatalogOnly,
        PresentInDatabaseOnly,
        DifferentInCatalog
    }
}
