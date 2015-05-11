// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using AnglicanGeek.DbExecutor;

namespace NuGetGallery.Operations
{
    [Command("fixexternalpackage", "Download the specified package which uses ExternalPackageUrl and transfer it to the storage server", AltName = "fep", IsSpecialPurpose = true)]
    public class FixExternalPackageTask : DatabasePackageVersionTask
    {
        public override void ExecuteCommand()
        {
            // todo: move the data access from the website to a common lib and use that instead
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();
                var package = dbExecutor.Query<Package>(
                    "SELECT p.[Key], pr.Id, p.Version, p.ExternalPackageUrl FROM Packages p JOIN PackageRegistrations pr ON pr.[Key] = p.PackageRegistrationKey WHERE pr.Id = @id AND p.Version = @version AND p.ExternalPackageUrl IS NOT NULL", 
                    new { id = PackageId, version = PackageVersion })
                    .SingleOrDefault();
                if (package == null)
                {
                    Log.Info("Package is stored locally: {0} {1}", PackageId, PackageVersion);
                }
                else
                {
                    using (var httpClient = new HttpClient())
                    using (var packageStream = httpClient.GetStreamAsync(package.ExternalPackageUrl).Result)
                    {
                        new UploadPackageTask
                        {
                            StorageAccount = StorageAccount,
                            PackageId = package.Id,
                            PackageVersion = package.Version,
                            PackageFile = packageStream,
                            WhatIf = WhatIf
                        }.ExecuteCommand();
                    }

                    if (!WhatIf)
                    {
                        dbExecutor.Execute(
                            "UPDATE Packages SET ExternalPackageUrl = NULL WHERE [Key] = @key",
                            new { key = package.Key });
                    }
                }
            }
        }
    }
}