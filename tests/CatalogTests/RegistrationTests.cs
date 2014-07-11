using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Registration;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTests
{
    class RegistrationTests
    {
        static async Task CreateRegistrationAsync(Storage storage, string resolverBaseAddress, string connectionString, string sql)
        {
            await CreateRegistrationAsync(storage, resolverBaseAddress, connectionString, sql, (entry) => entry.Id);
        }

        static async Task CreateRegistrationAsync(Storage storage, string resolverBaseAddress, string connectionString, string sql, Func<Entry, string> key)
        {
            RegistrationBuilder builder = new RegistrationBuilder(storage, 1000, true);

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand(sql, connection);

                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string id = reader.GetString(0);
                    string version = reader.GetString(1);
                    string description = reader.GetString(2);

                    try
                    {
                        builder.Add(new Entry
                        {
                            Uri = new Uri(resolverBaseAddress + id.ToLowerInvariant() + ".json#" + version),
                            Id = id,
                            Version = version,
                            Description = description
                        },
                        key);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("exception on id: {0})", id);
                        //throw;
                    }
                }
            }

            await builder.Commit();
        }
        static Storage CreateStorage(string name)
        {
            Storage storage = new FileStorage("http://localhost:8000/" + name + "/", @"c:\data\site\" + name);

            //CloudStorageAccount account = new CloudStorageAccount(new StorageCredentials(...), false);
            //Storage storage = new AzureStorage(account, name);

            return storage;
        }
        public static async Task Test0Async()
        {
            string resolverBaseAddress = "http://nugetdev0.blob.core.windows.net/cdn-public/v3/resolver/";
            string connectionString = "...";

            string islatest = @"
                SELECT PackageRegistrations.[Id], Packages.[NormalizedVersion], Packages.[Description] 
                FROM Packages
                INNER JOIN PackageRegistrations ON Packages.PackageRegistrationKey = PackageRegistrations.[Key]
                WHERE Packages.Listed = 1
                  AND Packages.IsLatest = 1
                ORDER BY PackageRegistrations.[Id]
            ";

            string islateststable = @"
                SELECT PackageRegistrations.[Id], Packages.[NormalizedVersion], Packages.[Description] 
                FROM Packages
                INNER JOIN PackageRegistrations ON Packages.PackageRegistrationKey = PackageRegistrations.[Key]
                WHERE Packages.Listed = 1
                  AND Packages.IsLatestStable = 1
                ORDER BY PackageRegistrations.[Id]
            ";

            string allversions = @"
                SELECT PackageRegistrations.[Id], Packages.[NormalizedVersion], Packages.[Description] 
                FROM Packages
                INNER JOIN PackageRegistrations ON Packages.PackageRegistrationKey = PackageRegistrations.[Key]
                WHERE Packages.Listed = 1
                ORDER BY PackageRegistrations.[Id]
            ";

            await CreateRegistrationAsync(CreateStorage("islatest"), resolverBaseAddress, connectionString, islatest);
            //await CreateRegistrationAsync(CreateStorage("islateststable"), resolverBaseAddress, connectionString, islateststable);
            //await CreateRegistrationAsync(CreateStorage("allversions"), resolverBaseAddress, connectionString, allversions);
        }

        public static void Test0()
        {
            Test0Async().Wait();
        }
    }
}
