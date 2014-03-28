using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Trigger = System.Collections.Generic.IDictionary<string, string>;

namespace MetadataClient
{
    public static class MetaDataTriggerConstants
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
        public const string OwnerName = "OwnerName";
        public const string OldOwnerName = "OldOwnerName";
        public const string NewOwnerName = "NewOwnerName";
    }

    public static class MetadataTrigger
    {
        private const string PackagesTable = "Packages";
        private const string PackageRegistrationOwnersTable = "PackageRegistrationOwners";
        private const string UsersTable = "Users";
        private const string MDPackagesTable = "MDPackages";
        private const string MDPackageRegOwnersTable = "MDPackageRegOwners";
        private const string MDOwnersTable = "MDOwners";
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
                    triggers.AddRange(await DetectUploadPackages(sql));
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

        public static async Task<IList<Trigger>> DetectUploadPackages(SqlConnectionStringBuilder sql)
        {
            return null;
        }
    }
}
