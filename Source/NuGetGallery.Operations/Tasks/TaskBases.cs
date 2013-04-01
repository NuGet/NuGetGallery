using System;
using System.Data.SqlClient;
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

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            if (CurrentEnvironment != null && StorageAccount == null)
            {
                StorageAccount = CurrentEnvironment.MainStorage;
            }
            ArgCheck.RequiredOrConfig(StorageAccount, "StorageAccount");
        }
    }

    public abstract class DatabaseTask : OpsTask
    {
        [Option("Connection string to the database server", AltName = "db")]
        public SqlConnectionStringBuilder ConnectionString { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            // Load defaults from environment
            if (CurrentEnvironment != null && ConnectionString == null)
            {
                ConnectionString = CurrentEnvironment.MainDatabase;
            }

            ArgCheck.RequiredOrConfig(ConnectionString, "ConnectionString");
        }
    }

    public abstract class WarehouseTask : OpsTask
    {
        [Option("Connection string to the warehouse database server", AltName = "db")]
        public SqlConnectionStringBuilder ConnectionString { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            // Load defaults from environment
            if (CurrentEnvironment != null && ConnectionString == null)
            {
                ConnectionString = CurrentEnvironment.WarehouseDatabase;
            }

            ArgCheck.RequiredOrConfig(ConnectionString, "ConnectionString");
        }
    }

    public abstract class ReportsTask : WarehouseTask
    {
        [Option("Connection string to the warehouse reports container", AltName = "wracc")]
        public CloudStorageAccount ReportStorage { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            // Load defaults from environment
            if (CurrentEnvironment != null && ReportStorage == null)
            {
                ReportStorage = CurrentEnvironment.ReportStorage;
            }

            ArgCheck.RequiredOrConfig(ReportStorage, "ReportStorage");
        }
    }

    public abstract class DatabaseAndStorageTask : StorageTask
    {
        [Option("Connection string to the database server", AltName = "db")]
        public SqlConnectionStringBuilder ConnectionString { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            // Load defaults from environment
            if (CurrentEnvironment != null && ConnectionString == null)
            {
                ConnectionString = CurrentEnvironment.MainDatabase;
            }

            ArgCheck.RequiredOrConfig(ConnectionString, "ConnectionString");
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

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            ArgCheck.Required(PackageId, "PackageId");
            ArgCheck.Required(PackageVersion, "PackageVersion");
            ArgCheck.Required(PackageHash, "PackageHash");
        }
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
