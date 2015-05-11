// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Data.SqlClient;
using System.IO;
using AnglicanGeek.DbExecutor;
using NuGet;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("replacepackagefile", "Replaces a specific package file with a file you specify", AltName = "rpf")]
    public class ReplacePackageFileTask : DatabasePackageVersionTask
    {
        [Option("The file to replace the package with", AltName = "r")]
        public Stream ReplacementFile { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            ArgCheck.Required(ReplacementFile, "ReplacementFile");
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

                if (package == null)
                {
                    Log.Info("Package '{0}.{1}' does not exist; exiting.");
                    return;
                }

                new BackupPackageFileTask {
                    StorageAccount = StorageAccount,
                    PackageId = package.Id,
                    PackageVersion = package.Version,
                    PackageHash = package.Hash
                }.ExecuteCommand();

                var hash = Util.GenerateHash(ReplacementFile.ReadAllBytes());
                Log.Info("Updating hash for package '{0}.{1}' to '{2}'", package.Id, package.Version, hash);
                dbExecutor.Execute(
                    "UPDATE Packages SET Hash = @hash WHERE [Key] = @key",
                    new { @key = package.Key, hash });

                Log.Info("Uploading replacement file for package '{0}.{1}'", package.Id, package.Version);
                ReplacementFile.Position = 0;
                new UploadPackageTask {
                    StorageAccount = StorageAccount,
                    PackageId = package.Id,
                    PackageVersion = package.Version,
                    PackageFile = ReplacementFile
                }.ExecuteCommand();
            }
        }
    }
}
