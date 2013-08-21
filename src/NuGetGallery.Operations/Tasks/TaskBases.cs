using System;
using System.Data.SqlClient;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Reflection;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery.Operations.Common;
using System.Data.Entity.Migrations.Infrastructure;
using AnglicanGeek.DbExecutor;
using NuGetGallery.Infrastructure;

namespace NuGetGallery.Operations
{
    // Base classes to ensure consistent naming of options

    public abstract class StorageTaskBase : OpsTask
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
                StorageAccount = GetStorageAccountFromEnvironment(CurrentEnvironment);
            }
            ArgCheck.RequiredOrConfig(StorageAccount, "StorageAccount");
        }

        protected abstract CloudStorageAccount GetStorageAccountFromEnvironment(DeploymentEnvironment environment);
    }

    public abstract class StorageTask : StorageTaskBase
    {
        protected override CloudStorageAccount GetStorageAccountFromEnvironment(DeploymentEnvironment environment)
        {
            return environment.MainStorage;
        }
    }

    public abstract class BackupStorageTask : StorageTaskBase
    {
        protected override CloudStorageAccount GetStorageAccountFromEnvironment(DeploymentEnvironment environment)
        {
            return environment.BackupStorage;
        }
    }

    public abstract class DatabaseTaskBase : OpsTask
    {
        [Option("Connection string to the relevant database server", AltName = "db")]
        public SqlConnectionStringBuilder ConnectionString { get; set; }

        protected string ServerName { get { return Util.GetDatabaseServerName(ConnectionString); } }
        protected string DatabaseName { get { return ConnectionString.InitialCatalog; } }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            // Load defaults from environment
            if (CurrentEnvironment != null && ConnectionString == null)
            {
                ConnectionString = GetConnectionFromEnvironment(CurrentEnvironment);
            }

            ArgCheck.RequiredOrConfig(ConnectionString, "ConnectionString");
        }

        protected void WithConnection(Action<SqlConnection> act)
        {
            WithConnection((c, _) => act(c));
        }

        protected void WithConnection(Action<SqlConnection, SqlExecutor> act)
        {
            using (var c = OpenConnection())
            using (var e = new SqlExecutor(c))
            {
                act(c, e);
            }
        }

        protected void WithMasterConnection(Action<SqlConnection> act)
        {
            WithMasterConnection((c, _) => act(c));
        }

        protected void WithMasterConnection(Action<SqlConnection, SqlExecutor> act)
        {
            using (var c = OpenMasterConnection())
            using (var e = new SqlExecutor(c))
            {
                act(c, e);
            }
        }

        protected SqlConnection OpenConnection()
        {
            var c = new SqlConnection(ConnectionString.ConnectionString);
            c.Open();
            return c;
        }

        protected SqlConnection OpenMasterConnection()
        {
            var cstr = Util.GetMasterConnectionString(ConnectionString.ConnectionString);
            var c = new SqlConnection(cstr);
            c.Open();
            return c;
        }

        protected abstract SqlConnectionStringBuilder GetConnectionFromEnvironment(DeploymentEnvironment environment);
    }

    public abstract class DatabaseTask : DatabaseTaskBase
    {
        protected override SqlConnectionStringBuilder GetConnectionFromEnvironment(DeploymentEnvironment environment)
        {
            return environment.MainDatabase;
        }
    }

    public abstract class WarehouseTask : DatabaseTaskBase
    {
        protected override SqlConnectionStringBuilder GetConnectionFromEnvironment(DeploymentEnvironment environment)
        {
            return environment.WarehouseDatabase;
        }
    }

    public abstract class MigrationsTask : DatabaseTask
    {
        public override void ExecuteCommand()
        {
            // Create the gateway instance
            var gateway = new GalleryGateway();

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
                ConnectionString = GetConnectionFromEnvironment(CurrentEnvironment);
            }

            ArgCheck.RequiredOrConfig(ConnectionString, "ConnectionString");
        }

        protected virtual SqlConnectionStringBuilder GetConnectionFromEnvironment(DeploymentEnvironment environment)
        {
          return environment.MainDatabase;
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
