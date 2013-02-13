using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    // Base classes to ensure consistent naming of options

    public abstract class StorageTask : OpsTask
    {
        [Option("The connection string to the storage server", AltName = "st")]
        public CloudStorageAccount StorageAccount { get; set; }

        protected string StorageAccountName
        {
            get { return StorageAccount.Credentials.AccountName; }
        }

        protected CloudBlobClient CreateBlobClient()
        {
            return StorageAccount.CreateCloudBlobClient();
        }

        public StorageTask()
        {
            string defaultCs = Environment.GetEnvironmentVariable("NUGET_GALLERY_MAIN_STORAGE");
            if (!String.IsNullOrEmpty(defaultCs))
            {
                StorageAccount = CloudStorageAccount.Parse(defaultCs);
            }
        }

        public override void ValidateArguments()
        {
            ArgCheck.RequiredOrEnv(StorageAccount, "StorageAccount", "NUGET_GALLERY_MAIN_STORAGE");
        }
    }

    public abstract class DatabaseTask : OpsTask
    {
        [Option("Connection string to the database server", AltName = "db")]
        public string ConnectionString { get; set; }

        public DatabaseTask()
        {
            // Load defaults from environment
            ConnectionString = Environment.GetEnvironmentVariable("NUGET_GALLERY_MAIN_CONNECTION_STRING");
        }

        public override void ValidateArguments()
        {
            ArgCheck.RequiredOrEnv(ConnectionString, "ConnectionString", "NUGET_GALLERY_MAIN_CONNECTION_STRING");
        }
    }

    public abstract class DatabaseAndStorageTask : StorageTask
    {
        [Option("Connection string to the database server", AltName = "db")]
        public string ConnectionString { get; set; }

        public DatabaseAndStorageTask()
        {
            // Load defaults from environment
            ConnectionString = Environment.GetEnvironmentVariable("NUGET_GALLERY_MAIN_CONNECTION_STRING");
        }

        public override void ValidateArguments()
        {
            ArgCheck.RequiredOrEnv(ConnectionString, "ConnectionString", "NUGET_GALLERY_MAIN_CONNECTION_STRING");
        }
    }

    public abstract class PackageVersionTask : StorageTask
    {
        [Option("The ID of the package", AltName = "p")]
        public string PackageId { get; set; }

        [Option("The Version of the package", AltName = "v")]
        public string PackageVersion { get; set; }

        [Option("The Hash of the package", AltName = "h")]
        public string PackageHash { get; set; }
    }

    public abstract class DatabasePackageVersionTask : DatabaseAndStorageTask
    {
        [Option("The ID of the package", AltName = "p")]
        public string PackageId { get; set; }

        [Option("The Version of the package", AltName = "v")]
        public string PackageVersion { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            ArgCheck.Required(PackageId, "PackageId");
            ArgCheck.Required(PackageVersion, "PackageVersion");
        }
    }
}
