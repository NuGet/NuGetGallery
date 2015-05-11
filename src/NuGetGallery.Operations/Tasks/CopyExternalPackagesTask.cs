// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Data.SqlClient;
using System.IO;
using System.Net.Http;
using AnglicanGeek.DbExecutor;
using Microsoft.WindowsAzure.Storage;

namespace NuGetGallery.Operations.Tasks
{
    [Command("copyexternalpackages", "Copies the nupkg file of any packages that were using ExternalPackageUrl to blob storage, in preparation for deprecating the ExternalPackageUrl feature.", AltName = "cpxp", IsSpecialPurpose = true)]
    public class CopyExternalPackagesTask : DatabaseAndStorageTask
    {
        public override void ExecuteCommand()
        {
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();

                var externalPackages = dbExecutor.Query<Package>(@"
                    SELECT pr.Id, p.Version, p.ExternalPackageUrl
                    FROM Packages p 
                        JOIN PackageRegistrations pr ON pr.[Key] = p.PackageRegistrationKey
                    WHERE p.ExternalPackageUrl IS NOT NULL
                    ORDER BY Id, Version");

                foreach (Package pkg in externalPackages)
                {
                    Console.WriteLine();
                    HttpClient client = new HttpClient();
                    var responseTask = client.GetAsync(pkg.ExternalPackageUrl);
                    var response = responseTask.Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Found broken package: " + response.StatusCode + "  " + pkg.ExternalPackageUrl);
                        Console.WriteLine("You should ask the package owner to unlist the package " + pkg.Id + " " + pkg.Version);
                    }

                    var bytesTask = response.Content.ReadAsByteArrayAsync();
                    byte[] bytes = bytesTask.Result;
                    var blobClient = CreateBlobClient();
                    var packagesBlobContainer = Util.GetPackagesBlobContainer(blobClient);
                    var packageFileBlob = Util.GetPackageFileBlob(
                        packagesBlobContainer,
                        pkg.Id,
                        pkg.Version);
                    var fileName = Util.GetPackageFileName(
                        pkg.Id,
                        pkg.Version);
                    if (packageFileBlob.Exists())
                    {
                        Console.WriteLine("SKIPPED! Package file blob " + fileName + " already exists");
                    }
                    else
                    {
                        Console.WriteLine("Saving the package file " + pkg.ExternalPackageUrl + " to blob storage as " + fileName);
                        if (!WhatIf)
                        {
                            packageFileBlob.UploadFromStream(
                                new MemoryStream(bytes),
                                AccessCondition.GenerateIfNoneMatchCondition("*"));
                        }
                    }
                }
            }
        }
    }
}
