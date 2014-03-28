using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

using Trigger = System.Collections.Generic.Dictionary<string, string>;

namespace MetadataClient
{
    public static class MDConstants
    {
        public const string Trigger = "Trigger";

        /// TRIGGER KEYS
        public const string UploadPackage = "UploadPackage";
        public const string EditPackage = "EditPackage";
        public const string DeletePackage = "DeletePackage";
        public const string AddOwner = "AddOwner";
        public const string RemoveOwner = "RemoveOwner";
        public const string RenameOwner = "RenameOwner";

        /// TRIGGER PARAMETER KEYS
        public const string PackageId = "PackageId";
        public const string PackageVersion = "PackageVersion";
        public const string Package = "Package";
        public const string OwnerName = "OwnerName";
        public const string OldOwnerName = "OldOwnerName";
        public const string NewOwnerName = "NewOwnerName";
    }

    public class PackageRef
    {
        public int Key { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string Hash { get; set; }
    }

    public static class MetadataTrigger
    {
        public const string PackagesTable = "Packages";
        public const string PackageRegistrationOwnersTable = "PackageRegistrationOwners";
        public const string UsersTable = "Users";
        public const string MDPackagesTable = "MDPackages";
        public const string MDPackageRegOwnersTable = "MDPackageRegOwners";
        public const string MDOwnersTable = "MDOwners";
        public static async Task Start(CloudStorageAccount blobAccount, CloudBlobContainer container, SqlConnectionStringBuilder sql)
        {
            Console.WriteLine("Started polling...");
            Console.WriteLine("Looking for changes in {0}/{1} ", sql.DataSource, sql.InitialCatalog);

            // The blobAccount and container can potentially be used to put the trigger information
            // on package blobs or otherwise. Not Important now

            while(true)
            {
                try
                {
                    DisplayTriggers(await DetectChanges(sql));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                Console.WriteLine('.');
                Thread.Sleep(500);
            }
        }

        public static async Task<IList<Trigger>> DetectChanges(SqlConnectionStringBuilder sql)
        {
            var triggers = new List<Trigger>();
            try
            {
                using (var connection = await sql.ConnectTo())
                {
                    Console.WriteLine("Connection to database in {0}/{1} obtained: {2}", connection.DataSource, connection.Database, connection.ClientConnectionId);
                    triggers.AddRange(await DetectUploadPackages(connection));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return triggers;
        }

        private static void DisplayTriggers(IList<Trigger> triggers)
        {
            if (triggers != null && triggers.Count > 0)
            {
                Console.WriteLine("---------- TRIGGERS ARE ----------");
                foreach (Trigger trigger in triggers)
                {
                    Console.WriteLine("-------------TRIGGER------------");
                    foreach (var pair in trigger)
                    {
                        Console.WriteLine("\t{0} : {1}", pair.Key, pair.Value);
                    }
                }
                Console.WriteLine("--------------------------------");
            }
        }

        private static Task ExecuteAsync(SqlConnection connection, string sql)
        {
            SqlCommand cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;
            return cmd.ExecuteNonQueryAsync();
        }

        public static async Task<IList<Trigger>> DetectUploadPackages(SqlConnection connection)
        {
            Console.Write("Detecting Upload Packages");
            var triggers = new List<Trigger>();

            string sql = @"	SELECT [Key], Description, Version, [Hash] FROM dbo.Packages WHERE [Key] NOT IN 
(SELECT PackageKey FROM dbo.MDPackages)";

            Console.WriteLine("Querying for new packages...");

            var newPackages = await connection.QueryAsync<PackageRef>(sql);

            Console.WriteLine("New Packages have been identified");

            int newPackageCount = 0;
            foreach (var package in newPackages)
            {
                Trigger trigger = new Trigger();
                trigger.Add(MDConstants.Trigger, MDConstants.UploadPackage);
                trigger.Add(MDConstants.PackageId, package.Description);
                trigger.Add(MDConstants.PackageVersion, package.Version);

                triggers.Add(trigger);
                newPackageCount++;
            }

            Console.WriteLine("{0} new package(s) were identified", newPackageCount);

            return triggers;
        }
    }
}
