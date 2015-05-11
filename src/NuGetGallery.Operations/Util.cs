// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web;
using AnglicanGeek.DbExecutor;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using NuGetGallery.Auditing;
using NuGetGallery.Operations.Model;

namespace NuGetGallery.Operations
{
    public static class Util
    {
        public const byte CopyingState = 7;
        public const byte OnlineState = 0;

        private static JsonSerializerSettings _auditRecordSerializerSettings = new JsonSerializerSettings()
        {
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DefaultValueHandling = DefaultValueHandling.Include,
            Formatting = Formatting.Indented,
            MaxDepth = 10,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include,
            TypeNameHandling = TypeNameHandling.None
        };
        
        public static bool BackupIsInProgress(SqlExecutor dbExecutor, string backupPrefix)
        {
            return dbExecutor.Query<Db>(
                // Not worried about SQL Injection here :). This is an admin tool.
                "SELECT name, state FROM sys.databases WHERE name LIKE '" + backupPrefix + "%' AND state = @state",
                new { state = CopyingState })
                .Any();
        }

        public static string DownloadPackage(
            CloudBlobContainer container,
            string id,
            string version,
            string folder)
        {
            var fileName = string.Format(
                "{0}.{1}.nupkg",
                id,
                version);
            var path = Path.Combine(folder, fileName);

            var blob = container.GetBlockBlobReference(fileName);
            blob.DownloadToFile(path);

            return path;
        }

        public static string GetDatabaseNameTimestamp(Db database)
        {
            return GetDatabaseNameTimestamp(database.Name);
        }

        public static string GetDatabaseNameTimestamp(string databaseName)
        {
            if (databaseName == null) throw new ArgumentNullException("databaseName");

            return databaseName.Substring("Backup_".Length);
        }

        public static DateTime GetDateTimeFromTimestamp(string timestamp)
        {
            DateTime result;
            if (DateTime.TryParseExact(timestamp, "yyyyMMddHHmmss", CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result))
            {
                return result;
            }
            else if (DateTime.TryParseExact(timestamp, "yyyyMMMdd_HHmmZ", CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result))
            {
                return result;
            }
            return DateTime.MinValue;
        }

        public static string GetDbName(string connectionString)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            return connectionStringBuilder.InitialCatalog;
        }

        public static string GetDbServer(string connectionString)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            return connectionStringBuilder.DataSource;
        }

        public static bool DatabaseExistsAndIsOnline(
            IDbExecutor dbExecutor,
            string restoreName)
        {
            var backupDbs = dbExecutor.Query<Db>(
                "SELECT name, state FROM sys.databases WHERE name = @restoreName AND state = @state",
                new { restoreName, state = OnlineState })
                .OrderByDescending(database => database.Name);

            return backupDbs.FirstOrDefault() != null;
        }

        public static Db GetLastBackup(SqlExecutor dbExecutor, string backupNamePrefix)
        {
            var allBackups = dbExecutor.Query<Db>(
                "SELECT name, state FROM sys.databases WHERE name LIKE '" + backupNamePrefix + "%' AND state = @state",
                new { state = OnlineState });
            var orderedBackups = from db in allBackups
                                 let t = OnlineDatabaseBackup.ParseTimestamp(db.Name)
                                 where t != null
                                 orderby t.Value descending
                                 select db;

            return orderedBackups.FirstOrDefault();
        }

        public static DateTime GetLastBackupTime(SqlExecutor dbExecutor, string backupNamePrefix)
        {
            var lastBackup = GetLastBackup(dbExecutor, backupNamePrefix);

            if (lastBackup == null)
                return DateTime.MinValue;

            var timestamp = lastBackup.Name.Substring(backupNamePrefix.Length);

            return GetDateTimeFromTimestamp(timestamp);
        }

        public static string GetMasterConnectionString(string connectionString)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
            return connectionStringBuilder.ToString();
        }

        public static string GetConnectionString(string connectionString, string databaseName)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = databaseName };
            return connectionStringBuilder.ToString();
        }

        public static string GetOpsConnectionString(string connectionString)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "NuGetGalleryOps" };
            return connectionStringBuilder.ToString();
        }

        public static string GetTimestamp()
        {
            return DateTime.UtcNow.ToString("yyyyMMMdd_HHmm") + "Z";
        }

        internal static CloudBlobContainer GetPackageBackupsBlobContainer(CloudBlobClient blobClient)
        {
            var container = blobClient.GetContainerReference("packagebackups");
            container.CreateIfNotExists();
            container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Off });
            return container;
        }

        internal static CloudBlobContainer GetPackagesBlobContainer(CloudBlobClient blobClient)
        {
            var container = blobClient.GetContainerReference("packages");
            return container;
        }

        internal static string GetPackageFileName(
            string id,
            string version)
        {
            return string.Format(
                "{0}.{1}.nupkg",
                id.ToLowerInvariant(),
                version.ToLowerInvariant());
        }

        internal static string GetTempFolder()
        {
            string ret = Path.Combine(Path.GetTempPath(), "NuGetGallery.Operations");
            if (!Directory.Exists(ret))
            {
                Directory.CreateDirectory(ret);
            }

            return ret;
        }

        internal static string GetPackageBackupFileName(
            string id,
            string version,
            string hash)
        {
            var hashBytes = Convert.FromBase64String(hash);

            return string.Format(
                "{0}/{1}/{2}.nupkg",
                id,
                version,
                HttpServerUtility.UrlTokenEncode(hashBytes));
        }

        public static string GetBackupOfOriginalPackageFileName(string id, string version)
        {
            return string.Format(
                "packagehistories/{0}/{0}.{1}.nupkg",
                id.ToLowerInvariant(),
                version.ToLowerInvariant());
        }

        internal static CloudBlockBlob GetPackageFileBlob(
            CloudBlobContainer packagesBlobContainer,
            string id,
            string version)
        {
            var packageFileName = GetPackageFileName(
                id,
                version);
            return packagesBlobContainer.GetBlockBlobReference(packageFileName);
        }

        internal static Package GetPackage(
            IDbExecutor dbExecutor,
            string id,
            string version)
        {
            return dbExecutor.Query<Package>(
                "SELECT p.[Key], pr.Id, p.Version, p.NormalizedVersion, p.Hash FROM Packages p JOIN PackageRegistrations pr ON pr.[Key] = p.PackageRegistrationKey WHERE pr.Id = @id AND p.Version = @version",
                new { id, version }).SingleOrDefault();
        }

        internal static PackageRegistration GetPackageRegistration(
            IDbExecutor dbExecutor,
            string id)
        {
            return dbExecutor.Query<PackageRegistration>(
                "SELECT [Key], Id FROM PackageRegistrations WHERE Id = @id",
                new { id }).SingleOrDefault();
        }

        internal static IEnumerable<Package> GetPackages(
            IDbExecutor dbExecutor,
            int packageRegistrationKey)
        {
            return dbExecutor.Query<Package>(
                "SELECT pr.Id, p.Version FROM Packages p JOIN PackageRegistrations PR on pr.[Key] = p.PackageRegistrationKey WHERE pr.[Key] = @packageRegistrationKey",
                new { packageRegistrationKey });
        }

        internal static User GetUser(
            IDbExecutor dbExecutor,
            string username)
        {
            var user = dbExecutor.Query<User>(
                "SELECT u.[Key], u.Username, u.EmailAddress, u.UnconfirmedEmailAddress FROM Users u WHERE u.Username = @username",
                new { username }).SingleOrDefault();

            if (user != null)
            {
                user.PackageRegistrationIds = dbExecutor.Query<string>(
                    "SELECT r.[Id] FROM PackageRegistrations r INNER JOIN PackageRegistrationOwners o ON o.PackageRegistrationKey = r.[Key] WHERE o.UserKey = @userKey AND NOT EXISTS(SELECT * FROM PackageRegistrationOwners other WHERE other.PackageRegistrationKey = r.[Key] AND other.UserKey != @userKey)",
                    new { userkey = user.Key });
            }

            return user;
        }

        public static string GenerateHash(byte[] input)
        {
            byte[] hashBytes;

            using (var hashAlgorithm = HashAlgorithm.Create("SHA512"))
            {
                hashBytes = hashAlgorithm.ComputeHash(input);
            }

            var hash = Convert.ToBase64String(hashBytes);
            return hash;
        }

        public static string GetDatabaseServerName(SqlConnectionStringBuilder connectionStringBuilder)
        {
            var dataSource = connectionStringBuilder.DataSource;
            if (dataSource.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
                dataSource = dataSource.Substring(4);
            var indexOfFirstPeriod = dataSource.IndexOf(".", StringComparison.Ordinal);

            if (indexOfFirstPeriod > -1)
                return dataSource.Substring(0, indexOfFirstPeriod);

            return dataSource;
        }

        public static Db GetDatabase(
            IDbExecutor dbExecutor,
            string databaseName)
        {
            var dbs = dbExecutor.Query<Db>(
                "SELECT name, state FROM sys.databases WHERE name = @databaseName",
                new { databaseName });

            return dbs.SingleOrDefault();
        }

        public static IList<CloudBlockBlob> CollectBlobs(Logger log, CloudBlobContainer container, string prefix, Func<CloudBlockBlob, bool> condition = null, int? countEstimate = null)
        {
            List<CloudBlockBlob> list;
            if (countEstimate.HasValue)
            {
                list = new List<CloudBlockBlob>(countEstimate.Value);
            }
            else
            {
                list = new List<CloudBlockBlob>();
            }

            BlobContinuationToken token = null;
            do
            {
                var segment = container.ListBlobsSegmented(
                    prefix,
                    useFlatBlobListing: true,
                    blobListingDetails: BlobListingDetails.Copy,
                    maxResults: null,
                    currentToken: token,
                    options: new BlobRequestOptions(),
                    operationContext: new OperationContext());
                var oldCount = list.Count;
                int total = 0;
                foreach (var blob in segment.Results.OfType<CloudBlockBlob>())
                {
                    if (condition == null || condition(blob))
                    {
                        list.Add(blob);
                    }
                    total++;
                }

                log.Info("Matched {0}/{1} blobs in current segment. Found {2} blobs so far...", list.Count - oldCount, total, list.Count);
                token = segment.ContinuationToken;
            } while (token != null);

            return list;
        }

        public static IEnumerable<CloudBlockBlob> EnumerateBlobs(Logger log, CloudBlobContainer container, string prefix, Func<CloudBlockBlob, bool> condition = null)
        {
            BlobContinuationToken token = null;
            do
            {
                var segment = container.ListBlobsSegmented(
                    prefix,
                    useFlatBlobListing: true,
                    blobListingDetails: BlobListingDetails.Copy,
                    maxResults: null,
                    currentToken: token,
                    options: new BlobRequestOptions(),
                    operationContext: new OperationContext());
                foreach (var blob in segment.Results.OfType<CloudBlockBlob>().Where(b => condition == null || condition(b)))
                {
                    yield return blob;
                }

                token = segment.ContinuationToken;
            } while (token != null);
        }

        internal static string GetPackageAuditBlobName(string id, string version, PackageAuditAction action)
        {
            // Audit Blob Name:
            //  /auditing/package/[id]/[version]/[action]-at-[datetime]
            return String.Format("package/{0}/{1}/{3}-{2}.json",
                id, version, action.ToString(), DateTime.UtcNow.ToString("O"));
        }

        internal static async Task<Uri> SaveAuditRecord(CloudStorageAccount storage, AuditRecord auditRecord)
        {
            string localIP = await AuditActor.GetLocalIP();
            CloudAuditingService audit = new CloudAuditingService(
                Environment.MachineName,
                localIP,
                storage.CreateCloudBlobClient().GetContainerReference("auditing"),
                onBehalfOfThunk: null);
            return await audit.SaveAuditRecord(auditRecord);
        }

        public static string GenerateStatusString(int total, ref int counter)
        {
            return String.Format(
                "{0:000000}/{1:000000} {2:00.0}%",
                ++counter,
                total,
                (((double)counter / (double)total) * 100.0));
        }
    }
}
