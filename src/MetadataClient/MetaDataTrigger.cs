using System;
using System.Text;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using Trigger = System.Collections.Generic.Dictionary<string, string>;
using Newtonsoft.Json.Linq;

namespace MetadataClient
{
    //
    // TODO : DELETE MDSqlQueries class and use stored procedures instead
    //
    public static class MDSqlQueries
    {
        public const string PackageOwners = @"SELECT UserName
FROM dbo.PackageRegistrationOwners T1
INNER JOIN dbo.Users T2
ON T1.UserKey = T2.[Key]
WHERE PackageRegistrationKey =@id";

        public const string NewPackages = @"SELECT PackageRegistrationKey, Id, Version FROM dbo.Packages T1
INNER JOIN dbo.PackageRegistrations T2
ON [PackageRegistrationKey] = T2.[Key]
WHERE T1.[Key] NOT IN 
(SELECT PackageKey FROM dbo.MDPackages)";

        public const string OwnersForPackageFormat = @"SELECT UserName FROM dbo.Users T1
WHERE [Key] IN
(SELECT UserKey FROM dbo.PackageRegistrationOwners T2
WHERE PackageRegistrationKey = {0})";

// HANDLES UPLOAD PACKAGES

        public const string PackageStateJoin = @"SELECT T1.[Key] as SourcePackageKey, PackageKey as BackupPackageKey,
       T1.LastEdited as SourceLastEdited, T2.LastEdited as BackupLastEdited,
       T2.Id, T2.[Version]
FROM dbo.Packages T1
FULL OUTER JOIN dbo.MDPackageState T2
ON T1.[Key] = T2.PackageKey
WHERE (T1.[Key] IS NULL) OR (T2.[PackageKey] IS NULL)
OR (T1.LastEdited IS NOT NULL AND T2.LastEdited IS NULL
OR T1.LastEdited != T2.LastEdited)";

        public const string PackageRecordsFromdboPackages = @"SELECT T1.[Key] as PackageKey, PackageRegistrationKey, Id, [Version]
FROM dbo.Packages T1
INNER JOIN dbo.PackageRegistrations T2
ON T1.[PackageRegistrationKey] = T2.[Key]
WHERE T1.[Key] IN @packageKeys";

        public const string PackageOwnerRecords = @"SELECT PackageRegistrationKey, UserName as OwnerName
FROM dbo.PackageRegistrationOwners T1
INNER JOIN dbo.Users T2
ON T1.UserKey = T2.[Key]
WHERE PackageRegistrationKey
IN @packageRegKeys";

        // INSERT QUERY
        public const string InsertNewPackages = @"INSERT INTO dbo.MDPackageState VALUES (@PackageKey, @Id, @Version, NULL)";

// HANDLES DELETE PACKAGES

        public const string PackagesUndeleted = @"SELECT Id FROM dbo.PackageRegistrations WHERE Id IN @packageIds";

// HANDLES EDIT PACKAGES

        public const string PackageEditMetadata = @"SELECT * FROM dbo.Packages WHERE [Key] IN @packageKeys";

        // UPDATE QUERY
        public const string UpdateEditedPackages = @"UPDATE dbo.MDPackageState SET LastEdited = @LastEdited WHERE PackageKey = @PackageKey";


// HANDLES ADD OWNERS

// HANDLES REMOVE OWNERS

// HANDLES RENAME OWNER

// HANDLES USERS SYNC?
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

    public class PackageStateJoin
    {
        public int? SourcePackageKey { get; set; }
        public int? BackupPackageKey { get; set; }
        public DateTime SourceLastEdited { get; set; }
        public DateTime BackupLastEdited { get; set; }
        public string Id { get; set; }
        public string Version { get; set; }

        public override string ToString()
        {
            return String.Format("{0}, {1}, {2}, {3}, {4}, {5}", SourcePackageKey, BackupPackageKey, SourceLastEdited, BackupLastEdited, Id, Version);
        }
    }

    public class PackageRecord
    {
        public int PackageKey { get; set; }
        public int PackageRegistrationKey { get; set; }
        public string Id { get; set; }
        public string Version { get; set; }

        public override string ToString()
        {
            return String.Format("{0}, {1}, {2}, {3}", PackageKey, PackageRegistrationKey, Id, Version);
        }
    }

    public class PackageOwnerRecord
    {
        public int PackageRegistrationKey { get; set; }
        public string OwnerName { get; set; }

        public override string ToString()
        {
            return String.Format("{0}, {1}", PackageRegistrationKey, OwnerName);
        }
    }

    public class PackageEditMetadata
    {
        public int Key { get; set; }
        public string Version { get; set; }
        public string Title { get; set; }
        public string Authors { get; set; }
        public string Copyright { get; set; }
        public string Description { get; set; }
        public string IconUrl { get; set; }
        public string LicenseUrl { get; set; }
        public string ProjectUrl { get; set; }
        public string ReleaseNotes { get; set; }
        public bool RequiresLicenseAcceptance { get; set; }
        public string Summary { get; set; }
        public string Tags { get; set; }
        public DateTime? LastEdited { get; set; }
    }

    public static class MetadataTrigger
    {
        private const int MaxTriggersPerKind = 1000;
        public const string PackagesTable = "Packages";
        public const string PackageRegistrationOwnersTable = "PackageRegistrationOwners";
        public const string UsersTable = "Users";
        public const string MDPackagesTable = "MDPackages";
        public const string MDPackageRegOwnersTable = "MDPackageRegOwners";
        public const string MDOwnersTable = "MDOwners";
        public static async Task Start(CloudStorageAccount blobAccount, CloudBlobContainer container, SqlConnectionStringBuilder sql, bool dumpToCloud)
        {
            Console.WriteLine("Started polling...");
            Console.WriteLine("Looking for changes in {0}/{1} ", sql.DataSource, sql.InitialCatalog);

            if (await container.CreateIfNotExistsAsync())
            {
                Console.WriteLine("Container created");
            }

            Container = container;
            DumpToCloud = dumpToCloud;

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

        public static CloudBlobContainer Container
        {
            private get;
            set;
        }

        public static bool DumpToCloud
        {
            get;
            private set;
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
                Console.WriteLine(ex.StackTrace);
            }

            return triggers;
        }

        private static async Task DumpTriggers(IList<Trigger> triggers, IList<string> keyProperties = null)
        {
            var dumpToCloud = DumpToCloud;
            if (!DumpToCloud || keyProperties == null || keyProperties.Count == 0 || triggers == null || triggers.Count == 0)
            {
                Console.WriteLine("Don't dump or No Key properties to dump the trigger");
                dumpToCloud = false;
            }

            if (triggers != null && triggers.Count > 0)
            {
                foreach (Trigger trigger in triggers)
                {
                    string triggerJson = JsonConvert.SerializeObject(trigger, new KeyValuePairConverter());

                    Console.WriteLine(triggerJson);
                    if (dumpToCloud)
                    {
                        var triggerId = GetTriggerId(trigger, keyProperties);
                        Console.WriteLine("Dumping to {0}", triggerId);
                        CloudBlockBlob blob = Container.GetBlockBlobReference(triggerId);
                        if (await blob.ExistsAsync())
                        {
                            Console.WriteLine("{0} already exists", triggerId);
                        }
                        else
                        {
                            using (var stream = new MemoryStream(Encoding.Default.GetBytes(triggerJson), false))
                            {
                                await blob.UploadFromStreamAsync(stream);
                            }
                        }
                    }
                }
            }
        }

        private static string GetTriggerId(Trigger trigger, IList<string> keyProperties)
        {
            StringBuilder triggerId = new StringBuilder();
            foreach (var key in keyProperties)
            {
                triggerId.Append(trigger[key]);
                triggerId.Append(".");
            }
            triggerId.Append("json");

            return triggerId.ToString();
        }

        private static void DumpListForDebugging<T>(IEnumerable<T> values)
        {
            Console.WriteLine("\n--------------");
            Console.WriteLine(typeof(T) + "s");
            foreach(var value in values)
            {
                Console.WriteLine(value);
            }
            Console.WriteLine("\n--------------");
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
                Console.WriteLine(ex.StackTrace);
            }

            return triggers;
        }

        private static async Task<IList<Trigger>> HandleUploadPackages(SqlConnection connection, List<int> packageKeys)
        {
            List<Trigger> triggers = new List<Trigger>();

            if (packageKeys.Count > MaxTriggersPerKind)
            {
                packageKeys.RemoveRange(MaxTriggersPerKind, packageKeys.Count - MaxTriggersPerKind);
            }

            var packageRecords = await connection.QueryAsync<PackageRecord>(MDSqlQueries.PackageRecordsFromdboPackages, new { packageKeys = packageKeys });
            var packageRegKeys = (from packageRecord in packageRecords
                                  select packageRecord.PackageRegistrationKey).Distinct();
            var packageOwnerRecords = await connection.QueryAsync<PackageOwnerRecord>(MDSqlQueries.PackageOwnerRecords, new { packageRegKeys = packageRegKeys });

            var packageOwners = GetOwners(packageRegKeys, packageOwnerRecords);

            // Add 'UploadPackage' triggers (Should contain PackageId, PackageVersion, Owners List)
            foreach (var package in packageRecords)
            {
                Trigger trigger = new Trigger();
                trigger.Add(MDConstants.Trigger, MDConstants.UploadPackage);
                trigger.Add(MDConstants.PackageId, package.Id);
                trigger.Add(MDConstants.PackageVersion, package.Version);
                trigger.Add(MDConstants.Owners, GetJSONArray(MDConstants.OwnerName, packageOwners[package.PackageRegistrationKey]));

                triggers.Add(trigger);
            }

            // Dump Triggers
            await DumpTriggers(triggers, new List<string> { MDConstants.PackageId, MDConstants.PackageVersion } );

            // Update PackageState with uploaded packages
            // TODO : MAKE ASYNC IF POSSIBLE
            connection.Execute(MDSqlQueries.InsertNewPackages, packageRecords);

            return triggers;
        }

        private static IDictionary<int, IEnumerable<string>> GetOwners(IEnumerable<int> packageRegKeys, IEnumerable<PackageOwnerRecord> packageOwnerRecords)
        {
            Dictionary<int, IEnumerable<string>> owners = new Dictionary<int, IEnumerable<string>>();
            foreach (var packageRegKey in packageRegKeys)
            {
                // The following if should always return false if packageRegKeys passed in are distinct
                if(!owners.ContainsKey(packageRegKey))
                {
                    var packageOwners = from packageOwnerRecord in packageOwnerRecords
                                        where packageOwnerRecord.PackageRegistrationKey == packageRegKey
                                        select packageOwnerRecord.OwnerName;

                    owners.Add(packageRegKey, packageOwners);
                }
            }
            return owners;
        }

        private static string GetJSONArray(string propertyName, IEnumerable<string> values)
        {
            JArray array = new JArray(
                from value in values
                select new JObject(new JProperty(propertyName, value)));

            return array.ToString();
        }

        private static async Task<IList<Trigger>> HandleDeletePackages(SqlConnection connection, List<PackageStateJoin> records)
        {
            List<Trigger> triggers = new List<Trigger>();
            // Add 'DeletePackage' triggers (Should contain PackageId, PackageVersion)
            // Add 'DeletePackageRegistration' triggers (Should contain PackageId)
            var packageIds = from record in records
                             select record.Id;

            var undeletedPackages = (await connection.QueryAsync<string>(MDSqlQueries.PackagesUndeleted, new { packageIds = packageIds })).ToList();

            var deletedPackages = new HashSet<string>();

            foreach (var record in records)
            {
                Trigger trigger = new Trigger();
                if (undeletedPackages.Contains(record.Id))
                {
                    // This is deletion of a version of a package
                    // which has other undeleted versions
                    trigger.Add(MDConstants.Trigger, MDConstants.DeletePackage);
                    trigger.Add(MDConstants.PackageId, record.Id);
                    trigger.Add(MDConstants.PackageVersion, record.Version);
                }
                else
                {
                    if (!deletedPackages.Contains(record.Id))
                    {
                        deletedPackages.Add(record.Id);
                        trigger.Add(MDConstants.Trigger, MDConstants.DeletePackageRegistration);
                        trigger.Add(MDConstants.PackageId, record.Id);
                    }
                }
            }

            // Dump Triggers
            await DumpTriggers(triggers);

            // Update PackageState with deleted packages
            // TODO

            return triggers;
        }

        private static async Task<IList<Trigger>> HandleEditPackages(SqlConnection connection, List<PackageStateJoin> records)
        {
            List<Trigger> triggers = new List<Trigger>();
            var packageKeys = from record in records
                              select record.SourcePackageKey;

            var packagesEditMetadata = await connection.QueryAsync<PackageEditMetadata>(MDSqlQueries.PackageEditMetadata, new { packageKeys = packageKeys });

            // Add 'EditPackage' triggers (Should contain PackageId, PackageVersion AND Package MD Info)
            // TODO : Package MD Info will be handled later
            foreach (var packageEditMetadata in packagesEditMetadata)
            {
                Trigger trigger = new Trigger();
                trigger.Add(MDConstants.Trigger, MDConstants.EditPackage);
                var packageId = (from record in records
                                where record.SourcePackageKey.Value == packageEditMetadata.Key
                                select record.Id).FirstOrDefault();
                trigger.Add(MDConstants.PackageId, packageId);
                AddEditPackageMetadataTrigger(trigger, packageEditMetadata);

                triggers.Add(trigger);
            }

            // Dump Triggers
            await DumpTriggers(triggers);

            // Update PackageState with edited packages
            // TODO : MAKE ASYNC IF POSSIBLE AND MAKE IT A SINGLE STATEMENT IF POSSIBLE
            foreach (var packageEditMetadata in packagesEditMetadata)
            {
                connection.Execute(MDSqlQueries.UpdateEditedPackages, new { LastEdited = packageEditMetadata.LastEdited, PackageKey = packageEditMetadata.Key });
            }

            return triggers;
        }

        private static void AddEditPackageMetadataTrigger(Trigger trigger, PackageEditMetadata packageEditMetadata)
        {
            Type packageEditMetadataType = typeof(PackageEditMetadata);
            foreach (PropertyInfo propertyInfo in packageEditMetadataType.GetProperties())
            {
                var value = propertyInfo.GetValue(packageEditMetadata);
                trigger.Add(propertyInfo.Name, value != null ? value.ToString() : String.Empty);
            }
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
