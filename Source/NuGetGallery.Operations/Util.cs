using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web;
using AnglicanGeek.DbExecutor;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery.Operations
{
    public static class Util
    {
        public const byte CopyingState = 7;
        public const byte OnlineState = 0;
        
        public static bool BackupIsInProgress(SqlExecutor dbExecutor)
        {
            return dbExecutor.Query<Database>(
                "SELECT name, state FROM sys.databases WHERE name LIKE 'Backup_%' AND state = @state",
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

        public static string GetDatabaseNameTimestamp(Database database)
        {
            return GetDatabaseNameTimestamp(database.Name);
        }

        public static string GetDatabaseNameTimestamp(string databaseName)
        {
            if (databaseName == null) throw new ArgumentNullException("databaseName");
            
            if (databaseName.Length < 14)
                throw new InvalidOperationException("Database name isn't long enough to contain a timestamp.");

            return databaseName.Substring(databaseName.Length - 14);
        }

        public static DateTime GetDateTimeFromTimestamp(string timestamp)
        {
            var year = Int32.Parse(new string(timestamp.Take(4).ToArray()));
            var month = Int32.Parse(new string(timestamp.Skip(4).Take(2).ToArray()));
            var day = Int32.Parse(new string(timestamp.Skip(6).Take(2).ToArray()));
            var hour = Int32.Parse(new string(timestamp.Skip(8).Take(2).ToArray()));
            var minute = Int32.Parse(new string(timestamp.Skip(10).Take(2).ToArray()));
            var second = Int32.Parse(new string(timestamp.Skip(12).Take(2).ToArray()));

            return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
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
            var backupDbs = dbExecutor.Query<Database>(
                "SELECT name, state FROM sys.databases WHERE name = @restoreName AND state = @state",
                new { restoreName, state = OnlineState })
                .OrderByDescending(database => database.Name);

            return backupDbs.FirstOrDefault() != null;
        }

        public static Database GetLastBackup(SqlExecutor dbExecutor)
        {
            var backupDbs = dbExecutor.Query<Database>(
                "SELECT name, state FROM sys.databases WHERE name LIKE 'Backup_%' AND state = @state",
                new { state = OnlineState })
                .OrderByDescending(database => database.Name);

            return backupDbs.FirstOrDefault();
        }

        public static DateTime GetLastBackupTime(SqlExecutor dbExecutor)
        {
            var lastBackup = GetLastBackup(dbExecutor);

            if (lastBackup == null)
                return DateTime.MinValue;

            var timestamp = lastBackup.Name.Substring(7);
            
            return GetDateTimeFromTimestamp(timestamp);
        }

        public static string GetMasterConnectionString(string connectionString)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString) {InitialCatalog = "master"};
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
            return DateTime.UtcNow.ToString("yyyyMMddHHmmss");
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
                "{0}.{1}.{2}.nupkg",
                id,
                version,
                HttpServerUtility.UrlTokenEncode(hashBytes));
        }

        internal static ICloudBlob GetPackageFileBlob(
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
                "SELECT p.[Key], pr.Id, p.Version, p.Hash FROM Packages p JOIN PackageRegistrations pr ON pr.[Key] = p.PackageRegistrationKey WHERE pr.Id = @id AND p.Version = @version",
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
            if (dataSource.StartsWith("tcp:"))
                dataSource = dataSource.Substring(4);
            var indexOfFirstPeriod = dataSource.IndexOf(".", StringComparison.Ordinal);
            
            if (indexOfFirstPeriod > -1)
                return dataSource.Substring(0, indexOfFirstPeriod);

            return dataSource;
        }

        public static Database GetDatabase(
            IDbExecutor dbExecutor,
            string databaseName)
        {
            var dbs = dbExecutor.Query<Database>(
                "SELECT name, state FROM sys.databases WHERE name = @databaseName",
                new { databaseName });

            return dbs.SingleOrDefault();
        }
    }
}
