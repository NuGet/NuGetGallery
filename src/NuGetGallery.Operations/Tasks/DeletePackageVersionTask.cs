// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using AnglicanGeek.DbExecutor;
using Microsoft.WindowsAzure.Storage;
using NuGetGallery.Auditing;
using NuGetGallery.Operations.Common;
using NuGetGallery.Operations.Model;

namespace NuGetGallery.Operations
{
    [Command("deletepackageversion", "Delete a specific package version", AltName = "dpv")]
    public class DeletePackageVersionTask : DatabasePackageVersionTask
    {
        [Option("Storage account in which to place audit records and backups, usually provided by the environment")]
        public CloudStorageAccount BackupStorage { get; set; }

        [Option("Set this flag to write the deletion audit record ONLY and not proceed with the deletion itself")]
        public bool AuditOnly { get; set; }

        [Option("The reason for the deletion ('owner request', 'license violation', etc.)", AltName = "r")]
        public string Reason { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            if (BackupStorage == null && CurrentEnvironment != null)
            {
                BackupStorage = CurrentEnvironment.BackupStorage;
            }
            ArgCheck.RequiredOrConfig(BackupStorage, "BackupStorage");

            ArgCheck.Required(Reason, "Reason");
        }

        public override void ExecuteCommand()
        {
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();

                var package = Util.GetPackage(
                    dbExecutor,
                    PackageId,
                    PackageVersion);

                // Multiple queries? Yes. Do I care? No.
                var packageRecord = new DataTable();
                using (SqlCommand cmd = sqlConnection.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = "SELECT * FROM Packages WHERE [Key] = @key";
                    cmd.Parameters.AddWithValue("@key", package.Key);
                    var result = cmd.ExecuteReader();
                    packageRecord.Load(result);
                }

                var registrationRecord = new DataTable();
                using (SqlCommand cmd = sqlConnection.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = "SELECT * FROM PackageRegistrations WHERE [ID] = @id";
                    cmd.Parameters.AddWithValue("@id", package.Id);
                    var result = cmd.ExecuteReader();
                    registrationRecord.Load(result);
                }

                // Write a delete audit record
                var auditRecord = new PackageAuditRecord(
                    package.Id, 
                    package.Version, 
                    package.Hash, 
                    packageRecord, 
                    registrationRecord, 
                    PackageAuditAction.Deleted, 
                    Reason);

                if (WhatIf)
                {
                    Log.Info("Would Write Audit Record to " + auditRecord.GetPath());
                }
                else
                {
                    Log.Info("Writing Audit Record");
                    var uri = Util.SaveAuditRecord(BackupStorage, auditRecord).Result;
                    Log.Info("Successfully wrote audit record to: " + uri.AbsoluteUri);
                }

                if (package == null)
                {
                    Log.Error("Package version does not exist: '{0}.{1}'", PackageId, PackageVersion);
                    return;
                }

                if (!AuditOnly)
                {
                    Log.Info(
                        "Deleting package data for '{0}.{1}'",
                        package.Id,
                        package.Version);

                    if (!WhatIf && !AuditOnly)
                    {
                        dbExecutor.Execute(
                            "DELETE pa FROM PackageAuthors pa JOIN Packages p ON p.[Key] = pa.PackageKey WHERE p.[Key] = @key",
                            new { key = package.Key });
                        dbExecutor.Execute(
                            "DELETE pd FROM PackageDependencies pd JOIN Packages p ON p.[Key] = pd.PackageKey WHERE p.[Key] = @key",
                            new { key = package.Key });
                        dbExecutor.Execute(
                            "DELETE ps FROM PackageStatistics ps JOIN Packages p ON p.[Key] = ps.PackageKey WHERE p.[Key] = @key",
                            new { key = package.Key });
                        dbExecutor.Execute(
                            "DELETE pf FROM PackageFrameworks pf JOIN Packages p ON p.[Key] = pf.Package_Key WHERE p.[Key] = @key",
                            new { key = package.Key });
                        dbExecutor.Execute(
                            "DELETE p FROM Packages p JOIN PackageRegistrations pr ON pr.[Key] = p.PackageRegistrationKey WHERE p.[Key] = @key",
                            new { key = package.Key });
                    }

                    new DeletePackageFileTask
                    {
                        BackupStorage = BackupStorage,
                        StorageAccount = StorageAccount,
                        PackageId = package.Id,
                        PackageVersion = package.NormalizedVersion,
                        PackageHash = package.Hash,
                        WhatIf = WhatIf
                    }.ExecuteCommand();
                }
                else
                {
                    Log.Info("Only wrote audit record. Package was NOT deleted.");
                }
            }
        }
    }
}
