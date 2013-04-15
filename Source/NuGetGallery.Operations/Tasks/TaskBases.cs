using System;
using System.Data.SqlClient;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Reflection;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery.Operations.Common;
using System.Data.Entity.Migrations.Infrastructure;

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

    public abstract class MigrationsTask : DatabaseTask
    {
        private const string DefaultGatewayType = "NuGetGallery.Infrastructure.GalleryGateway";

        [Option("Path to the assembly containing the migrations", AltName = "a")]
        public string GalleryAssembly { get; set; }

        [Option("The type that will serve as a Gateway into the Gallery code. Usually the default value is fine", AltName = "t")]
        public string GatewayType { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            if (String.IsNullOrEmpty(GatewayType))
            {
                GatewayType = DefaultGatewayType;
            }

            ArgCheck.Required(GalleryAssembly, "GalleryAssembly");
        }

        public override void ExecuteCommand()
        {
            // Load the assembly and find the configuration type
            Assembly asm = Assembly.LoadFrom(GalleryAssembly);
            Type configType = asm.GetType(GatewayType);
            if (configType == null)
            {
                Log.Error("Could not find gateway type: {0}", GatewayType);
                return;
            }

            // Create the gateway instance
            dynamic gateway = Activator.CreateInstance(configType);
            
            // Get a migrator from it
            DbMigrator migrator = gateway.CreateMigrator(ConnectionString.ConnectionString, "System.Data.SqlClient");

            // Run the rest of the command
            ExecuteCommandCore(migrator);
        }

        protected abstract void ExecuteCommandCore(MigratorBase migrator);
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
