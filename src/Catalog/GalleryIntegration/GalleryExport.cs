using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace NuGet.Services.Metadata.Catalog.GalleryIntegration
{
    public class GalleryExport
    {
        public static Tuple<int, int> GetNextRange(string sqlConnectionString, int lastHighestPackageKey, int chunkSize)
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

        public static void FetchRange(string sqlConnectionString, Tuple<int, int> range, GalleryExportBatcher batcher)
        {
            IDictionary<int, JObject> packages = FetchPackages(sqlConnectionString, range);
            IDictionary<int, string> registrations = FetchPackageRegistrations(sqlConnectionString, range);
            IDictionary<int, List<JObject>> dependencies = FetchPackageDependencies(sqlConnectionString, range);
            IDictionary<int, List<string>> targetFrameworks = FetchPackageFrameworks(sqlConnectionString, range);

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

                batcher.Process(packages[key], registration, dependency, targetFramework);
            }
        }

        public static IDictionary<int, JObject> FetchPackages(string sqlConnectionString, Tuple<int, int> range)
        {
            using (SqlConnection connection = new SqlConnection(sqlConnectionString))
            {
                connection.Open();

                string cmdText = @"SELECT * FROM Packages WHERE [Key] >= @MinKey AND [Key] <= @MaxKey";

                SqlCommand command = new SqlCommand(cmdText, connection);
                command.Parameters.AddWithValue("MinKey", range.Item1);
                command.Parameters.AddWithValue("MaxKey", range.Item2);

                SqlDataReader reader = command.ExecuteReader();

                IDictionary<int, JObject> packages = new Dictionary<int, JObject>();

                while (reader.Read())
                {
                    int key = reader.GetInt32(reader.GetOrdinal("Key"));

                    JObject obj = new JObject();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        obj.Add(reader.GetName(i), new JValue(reader.GetValue(i)));
                    }

                    packages.Add(key, obj);
                }

                return packages;
            }
        }

        public static IDictionary<int, string> FetchPackageRegistrations(string sqlConnectionString, Tuple<int, int> range)
        {
            using (SqlConnection connection = new SqlConnection(sqlConnectionString))
            {
                connection.Open();

                string cmdText = @"
                    SELECT Packages.[Key] 'Key', PackageRegistrations.[Id] 'Id'
                    FROM PackageRegistrations 
                    INNER JOIN Packages ON PackageRegistrations.[Key] = Packages.[PackageRegistrationKey]
                    WHERE Packages.[Key] >= @MinKey AND Packages.[Key] <= @MaxKey";

                SqlCommand command = new SqlCommand(cmdText, connection);
                command.Parameters.AddWithValue("MinKey", range.Item1);
                command.Parameters.AddWithValue("MaxKey", range.Item2);

                SqlDataReader reader = command.ExecuteReader();

                IDictionary<int, string> registrations = new Dictionary<int, string>();

                while (reader.Read())
                {
                    int key = reader.GetInt32(reader.GetOrdinal("Key"));
                    string id = reader.GetString(reader.GetOrdinal("Id"));

                    registrations.Add(key, id);
                }

                return registrations;
            }
        }

        public static IDictionary<int, List<JObject>> FetchPackageDependencies(string sqlConnectionString, Tuple<int, int> range)
        {
            using (SqlConnection connection = new SqlConnection(sqlConnectionString))
            {
                connection.Open();

                string cmdText = @"
                    SELECT
                        Packages.[Key] 'Key',
                        PackageDependencies.[Id] 'Id',
                        PackageDependencies.VersionSpec 'VersionSpec',
                        ISNULL(PackageDependencies.TargetFramework, '') 'TargetFramework'
                    FROM PackageDependencies
                    INNER JOIN Packages ON PackageDependencies.[PackageKey] = Packages.[Key]
                    WHERE Packages.[Key] >= @MinKey AND Packages.[Key] <= @MaxKey";

                SqlCommand command = new SqlCommand(cmdText, connection);
                command.Parameters.AddWithValue("MinKey", range.Item1);
                command.Parameters.AddWithValue("MaxKey", range.Item2);

                SqlDataReader reader = command.ExecuteReader();

                IDictionary<int, List<JObject>> dependencies = new Dictionary<int, List<JObject>>();

                while (reader.Read())
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

        public static IDictionary<int, List<string>> FetchPackageFrameworks(string sqlConnectionString, Tuple<int, int> range)
        {
            using (SqlConnection connection = new SqlConnection(sqlConnectionString))
            {
                connection.Open();

                string cmdText = @"
                    SELECT
                        Packages.[Key] 'Key',
                        PackageFrameworks.TargetFramework 'TargetFramework'
                    FROM PackageFrameworks
                    INNER JOIN Packages ON PackageFrameworks.[Package_Key] = Packages.[Key]
                    WHERE Packages.[Key] >= @MinKey AND Packages.[Key] <= @MaxKey";

                SqlCommand command = new SqlCommand(cmdText, connection);
                command.Parameters.AddWithValue("MinKey", range.Item1);
                command.Parameters.AddWithValue("MaxKey", range.Item2);

                SqlDataReader reader = command.ExecuteReader();

                IDictionary<int, List<string>> targetFrameworks = new Dictionary<int, List<string>>();

                while (reader.Read())
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
    }
}
