// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Data.Entity.Migrations.Infrastructure;
using System.Data.SqlClient;
using System.Reflection;
using AnglicanGeek.DbExecutor;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery.Operations.Common;
using NuGetGallery.Infrastructure;
using Dapper;
using System.Security.Cryptography.X509Certificates;

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

    public abstract class DiagnosticsStorageTask : StorageTaskBase
    {
        protected override CloudStorageAccount GetStorageAccountFromEnvironment(DeploymentEnvironment environment)
        {
            return environment.DiagnosticsStorage;
        }
    }

    public abstract class DatabaseTaskBase : OpsTask
    {
        [Option("Connection string to the relevant database server", AltName = "db")]
        public SqlConnectionStringBuilder ConnectionString { get; set; }

        [Option("Instead of -db, use this parameter to connect to a SQL LocalDb database of the specified name", AltName="ldb")]
        public string LocalDbName { get; set; }

        protected string ServerName { get { return Util.GetDatabaseServerName(ConnectionString); } }


        public override void ValidateArguments()
        {
            base.ValidateArguments();

            // Load defaults from environment
            if(ConnectionString == null) {
                if (CurrentEnvironment != null)
                {
                    ConnectionString = GetConnectionFromEnvironment(CurrentEnvironment);
                }
            }
            
            // Local Db Name overrides others
            if (!String.IsNullOrEmpty(LocalDbName))
            {
                ConnectionString = new SqlConnectionStringBuilder()
                {
                    DataSource = @"(LocalDB)\v11.0",
                    IntegratedSecurity = true,
                    InitialCatalog = LocalDbName
                };
                Log.Info("Using LocalDB connection: {0}", ConnectionString.ConnectionString);
            }

            ArgCheck.RequiredOrConfig(ConnectionString, "ConnectionString");
        }

        protected void WithConnection(Action<SqlConnection> act)
        {
            WithConnection((c, _) => act(c));
        }

        protected void WithConnection(Action<SqlConnection, SqlExecutor> act)
        {
            WithConnection((c, e) => { act(c, e); return true; });
        }

        protected bool WithConnection(Func<SqlConnection, SqlExecutor, bool> act)
        {
            using (var c = OpenConnection())
            using (var e = new SqlExecutor(c))
            {
                return act(c, e);
            }
        }

        protected void WithMasterConnection(Action<SqlConnection> act)
        {
            WithMasterConnection((c, _) => act(c));
        }

        protected void WithMasterConnection(Action<SqlConnection, SqlExecutor> act)
        {
            WithMasterConnection((c, e) => { act(c, e); return true; });
        }

        protected bool WithMasterConnection(Func<SqlConnection, SqlExecutor, bool> act)
        {
            using (var c = OpenMasterConnection())
            using (var e = new SqlExecutor(c))
            {
                return act(c, e);
            }
        }

        protected static void WithTableType(SqlConnection connection, string name, string definition, Action act)
        {
            try
            {
                // Create the table-valued parameter type
                connection.Execute(String.Format(@"
                        IF EXISTS (
                            SELECT * 
                            FROM sys.types 
                            WHERE is_table_type = 1 
                            AND name = '{0}'
                        )
                        BEGIN
                            DROP TYPE {0}
                        END
                        CREATE TYPE {0} AS TABLE ({1})", name, definition));

                act();
            }
            finally
            {
                // Clean up the table-valued parameter type
                connection.Execute(String.Format(@"
                        IF EXISTS (
                            SELECT * 
                            FROM sys.types 
                            WHERE is_table_type = 1 
                            AND name = '{0}'
                        )
                        BEGIN
                            DROP TYPE {0}
                        END", name));
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

    public abstract class DatabaseAndStorageTask : DatabaseTask
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

    public abstract class SubscriptionTask : OpsTask
    {
        [Option("The subscription ID to use", AltName = "s")]
        public string SubscriptionId { get; set; }

        [Option("The management certificate thumbprint to use for authentication", AltName = "t")]
        public string Thumbprint { get; set; }

        public X509Certificate2 ManagementCertificate { get; private set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            if (CurrentEnvironment != null)
            {
                if (String.IsNullOrEmpty(SubscriptionId))
                {
                    SubscriptionId = CurrentEnvironment.SubscriptionId;
                }
            }
            ArgCheck.RequiredOrConfig(SubscriptionId, "SubscriptionId");

            var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            if (String.IsNullOrEmpty(Thumbprint))
            {
                ManagementCertificate = store.Certificates
                    .Find(X509FindType.FindBySubjectName, SubscriptionId, validOnly: false)
                    .OfType<X509Certificate2>()
                    .FirstOrDefault();
                if (ManagementCertificate == null)
                {
                    throw new CommandLineException("Could not find the default management certificate for the subscription. Specify the -Thumbprint argument to select a certificate");
                }
            }
            else
            {
                ManagementCertificate = store.Certificates
                    .Find(X509FindType.FindByThumbprint, Thumbprint, validOnly: false)
                    .OfType<X509Certificate2>()
                    .FirstOrDefault();
                if (ManagementCertificate == null)
                {
                    throw new CommandLineException("Could not find a management certificate with the provided thumbprint.");
                }
            }
        }
    }
}
