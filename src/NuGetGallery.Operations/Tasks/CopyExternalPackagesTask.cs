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

                    var contentStream = response.Content.ReadAsStreamAsync().Result;
                    if (contentStream.CanSeek)
                    {
                        // may not be necessary?
                        contentStream.Seek(0, SeekOrigin.Begin);
                    }

                    var packageFiles = GetPackageFileService();
                    var fileName = FileConventions.GetPackageFileName(pkg.Id, pkg.Version);
                    if (packageFiles.PackageFileExists(pkg.Id, pkg.Version))
                    {
                        Console.WriteLine("SKIPPED! Package file blob " + fileName + " already exists");
                    }
                    else
                    {
                        Console.WriteLine("Saving the package file " + pkg.ExternalPackageUrl + " to blob storage as " + fileName);
                        if (!WhatIf)
                        {
                            packageFiles.SavePackageFileAsync(pkg.Id, pkg.Version, contentStream).Wait();
                        }
                    }
                }
            }
        }
    }
}
