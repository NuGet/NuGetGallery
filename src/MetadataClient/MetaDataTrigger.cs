using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

using Trigger = System.Collections.Generic.Dictionary<string, string>;

namespace MetadataClient
{
    //
    // TODO : DELETE MDSqlQueries class and use stored procedures instead
    //
    public static class MDSqlQueries
    {
        public const string NewPackages = @"SELECT PackageRegistrationKey, Id, Version FROM dbo.Packages T1
INNER JOIN dbo.PackageRegistrations T2
ON [PackageRegistrationKey] = T2.[Key]
WHERE T1.[Key] NOT IN 
(SELECT PackageKey FROM dbo.MDPackages)";

        public const string OwnersForPackageFormat = @"SELECT UserName FROM dbo.Users T1
WHERE [Key] IN
(SELECT UserKey FROM dbo.PackageRegistrationOwners T2
WHERE PackageRegistrationKey = {0})";

        public const string PackageStateJoin = @"SELECT T1.[Key] as SourcePackageKey, PackageKey as BackupPackageKey,
       T1.LastEdited as SourceLastEdited, T2.LastEdited as BackupLastEdited,
       T2.Id, T2.[Version]
FROM dbo.Packages T1
FULL OUTER JOIN dbo.MDPackageState T2
ON T1.[Key] = T2.PackageKey
WHERE (T1.[Key] IS NULL) OR (T2.[PackageKey] IS NULL)
OR (T1.LastEdited IS NOT NULL AND T2.LastEdited IS NULL
OR T1.LastEdited != T2.LastEdited)";

        public const string PackageRegRef = @"SELECT PackageRegistrationKey, Id, [Version]
FROM dbo.Packages T1
INNER JOIN dbo.PackageRegistrations T2
ON T1.[PackageRegistrationKey] = T2.[Key]
WHERE T1.[Key] IN @records";
    }

    public static class MDConstants
    {
        public const string Trigger = "Trigger";

        /// TRIGGER KEYS
        public const string UploadPackage = "UploadPackage";
        public const string EditPackage = "EditPackage";
        public const string DeletePackage = "DeletePackage";
        public const string DeletePackageRegistration = "DeletePackageRegistration";
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
        public const string Owners = "Owners";
    }

    public class PackageRegRef
    {
        public int PackageRegistrationKey { get; set; }
        public string Id { get; set; }
        public string Version { get; set; }
    }

    public class PackageStateJoin
    {
        public int? SourcePackageKey { get; set; }
        public int? BackupPackageKey { get; set; }
        public DateTime SourceLastEdited { get; set; }
        public DateTime BackupLastEdited { get; set; }
        public string Id { get; set; }
        public string Version { get; set; }
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
                    await DetectChanges(sql);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                Console.WriteLine('.');
                Thread.Sleep(3000);
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
                    triggers.AddRange(await DetectPackageChanges(connection));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return triggers;
        }

        private static void DumpTriggers(IList<Trigger> triggers)
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

        public static async Task<IList<Trigger>> DetectPackageChanges(SqlConnection connection)
        {
            List<Trigger> triggers = new List<Trigger>();

            try
            {
                Console.WriteLine("Starting Package changes detection");

                var packageStateJoin = await connection.QueryAsync<PackageStateJoin>(MDSqlQueries.PackageStateJoin);

                Console.WriteLine("Package changes detection completed");

                List<int> uploadPackageRecords = new List<int>();
                List<PackageStateJoin> deletedPackageRecords = new List<PackageStateJoin>();
                List<PackageStateJoin> editedPackageRecords = new List<PackageStateJoin>();

                foreach (var record in packageStateJoin)
                {
                    if (!record.BackupPackageKey.HasValue && !record.SourcePackageKey.HasValue)
                    {
                        throw new InvalidOperationException("Both source package key and backup package key cannot be NULL");
                    }

                    if (!record.BackupPackageKey.HasValue && record.SourcePackageKey.HasValue)
                    {                        
                        uploadPackageRecords.Add(record.SourcePackageKey.Value);
                    }
                    else if (!record.SourcePackageKey.HasValue && record.BackupPackageKey.HasValue)
                    {
                        deletedPackageRecords.Add(record);
                    }
                    else if (record.BackupLastEdited != record.SourceLastEdited)
                    {
                        editedPackageRecords.Add(record);
                    }
                }

                // Add 'UploadPackage' triggers (Should contain PackageId, PackageVersion, Owners List)
                var uploadTriggers = await HandleUploadPackages(connection, uploadPackageRecords);
                triggers.AddRange(uploadTriggers);

                // Add 'DeletePackage' triggers (Should contain PackageId, PackageVersion)
                // Add 'DeletePackageRegistration' triggers (Should contain PackageId)
                var deleteTriggers = await HandleDeletePackages(connection, deletedPackageRecords);
                triggers.AddRange(deleteTriggers);


                // Add 'EditPackage' triggers (Should contain PackageId, PackageVersion AND Package MD Info)
                // TODO : Package MD Info will be handled later
                var editTriggers = await HandleEditPackages(connection, editedPackageRecords);
                triggers.AddRange(editTriggers);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return triggers;
        }

        private static async Task<IList<Trigger>> HandleUploadPackages(SqlConnection connection, List<int> records)
        {
            List<Trigger> triggers = new List<Trigger>();

            var packageRegRefs = await connection.QueryAsync<PackageRegRef>(MDSqlQueries.PackageRegRef, new { records = records });
            // Add 'UploadPackage' triggers (Should contain PackageId, PackageVersion, Owners List)
            foreach (var package in packageRegRefs)
            {
                Trigger trigger = new Trigger();
                trigger.Add(MDConstants.Trigger, MDConstants.UploadPackage);
                trigger.Add(MDConstants.PackageId, package.Id);
                trigger.Add(MDConstants.PackageVersion, package.Version);

                triggers.Add(trigger);
            }

            // Dump Triggers
            DumpTriggers(triggers);

            // Update PackageState with uploaded packages
            // TODO

            return triggers;
        }

        private static async Task<IList<Trigger>> HandleDeletePackages(SqlConnection connection, List<PackageStateJoin> records)
        {
            List<Trigger> triggers = new List<Trigger>();
            // Add 'DeletePackage' triggers (Should contain PackageId, PackageVersion)

            // Add 'DeletePackageRegistration' triggers (Should contain PackageId)

            // Dump Triggers

            // Update PackageState with deleted packages
            return triggers;
        }

        private static async Task<IList<Trigger>> HandleEditPackages(SqlConnection connection, List<PackageStateJoin> records)
        {
            List<Trigger> triggers = new List<Trigger>();
            // Add 'EditPackage' triggers (Should contain PackageId, PackageVersion AND Package MD Info)
            // TODO : Package MD Info will be handled later
            foreach(var record in records)
            {
                Trigger trigger = new Trigger();
                trigger.Add(MDConstants.Trigger, MDConstants.EditPackage);
                trigger.Add(MDConstants.PackageId, record.Id);
                trigger.Add(MDConstants.PackageVersion, record.Version);

                triggers.Add(trigger);
            }

            // Dump Triggers
            DumpTriggers(triggers);

            // Update PackageState with edited packages
            // TODO

            return triggers;
        }

        //public static async Task<IList<Trigger>> DetectUploadPackages(SqlConnection connection)
        //{
        //    Console.Write("Detecting Upload Packages");
        //    var triggers = new List<Trigger>();

        //    Console.WriteLine("Querying for new packages...");

        //    var newPackages = await connection.QueryAsync<PackageRef>(MDSqlQueries.NewPackages);

        //    Console.WriteLine("New Packages have been identified");

        //    int newPackageCount = 0;
        //    foreach (var package in newPackages)
        //    {
        //        Trigger trigger = new Trigger();
        //        trigger.Add(MDConstants.Trigger, MDConstants.UploadPackage);
        //        trigger.Add(MDConstants.PackageId, package.Id);
        //        trigger.Add(MDConstants.PackageVersion, package.Version);

        //        var owners = await 

        //        triggers.Add(trigger);
        //        newPackageCount++;
        //    }

        //    Console.WriteLine("{0} new package(s) were identified", newPackageCount);

        //    return triggers;
        //}
    }
}
