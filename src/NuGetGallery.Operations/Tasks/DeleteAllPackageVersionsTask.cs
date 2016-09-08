// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Data.SqlClient;
using AnglicanGeek.DbExecutor;
using Microsoft.WindowsAzure.Storage;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("deletefullpackage", "Delete all versions of the specified package", AltName = "dfp")]
    public class DeleteAllPackageVersionsTask : DatabaseAndStorageTask
    {
        [Option("Storage account in which to place audit records and backups, usually provided by the environment")]
        public CloudStorageAccount BackupStorage { get; set; }

        [Option("The ID of the package", AltName = "p")]
        public string PackageId { get; set; }

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

            ArgCheck.Required(PackageId, "PackageId");
        }

        public override void ExecuteCommand()
        {
            Log.Info(
                "Deleting package registration and all package versions for '{0}'.",
                PackageId);

            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();

                var packageRegistration = Util.GetPackageRegistration(
                    dbExecutor,
                    PackageId);
                var packages = Util.GetPackages(
                    dbExecutor,
                    packageRegistration.Key);

                foreach(var package in packages)
                {
                    var task = new DeletePackageVersionTask {
                        ConnectionString = ConnectionString,
                        BackupStorage = BackupStorage,
                        StorageAccount = StorageAccount,
                        PackageId = package.Id,
                        PackageVersion = package.Version,
                        Reason = Reason,
                        WhatIf = WhatIf
                    };
                    task.ExecuteCommand();
                }

                Log.Info(
                    "Deleting package registration data for '{0}'",
                    packageRegistration.Id);
                if (!WhatIf)
                {
                    dbExecutor.Execute(
                        "DELETE por FROM PackageOwnerRequests por JOIN PackageRegistrations pr ON pr.[Key] = por.PackageRegistrationKey WHERE pr.[Key] = @packageRegistrationKey",
                        new { packageRegistrationKey = packageRegistration.Key });
                    dbExecutor.Execute(
                        "DELETE pro FROM PackageRegistrationOwners pro JOIN PackageRegistrations pr ON pr.[Key] = pro.PackageRegistrationKey WHERE pr.[Key] = @packageRegistrationKey",
                        new { packageRegistrationKey = packageRegistration.Key });
                    dbExecutor.Execute(
                        "DELETE FROM PackageRegistrations WHERE [Key] = @packageRegistrationKey",
                        new { packageRegistrationKey = packageRegistration.Key });
                }
            }

            Log.Info(
                "Deleted package registration and all package versions for '{0}'.",
                PackageId);
        }
    }
}
