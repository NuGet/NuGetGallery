// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnglicanGeek.DbExecutor;
using NuGet;

namespace NuGetGallery.Operations.CuratedFeeds
{
    [Command("curatewebmatrix", "Runs the WebMatrix Curator on the specified storage server", AltName = "cwm", IsSpecialPurpose = true)]
    public class CurateWebmatrixPackagesTask : DatabaseAndStorageTask
    {
        private readonly string _tempFolder;

        public CurateWebmatrixPackagesTask()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "NuGetGalleryOps");
            Directory.CreateDirectory(_tempFolder);
        }

        public override void ExecuteCommand()
        {
            Log.Trace("Getting latest packages...");
            var packages = GetLatestStablePackages();
            Log.Trace("Getting previously curated packages...");
            var alreadyCuratedPackageIds = GetAlreadyCuratedPackageIds();
            Log.Trace("Calculating minimum difference set...");
            var packageIdsToCurate = packages.Keys.Except(alreadyCuratedPackageIds).ToList();

            var totalCount = packageIdsToCurate.Count;
            var processedCount = 0;
            Log.Trace(
                "Curating {0} packages for the WebMatrix curated on '{1}',",
                totalCount,
                ConnectionString);

            Parallel.ForEach(packageIdsToCurate, new ParallelOptions { MaxDegreeOfParallelism = 10 }, packageIdToCurate =>
            {
                var package = packages[packageIdToCurate];

                try
                {
                    var downloadPath = DownloadPackage(package);
                    var nugetPackage = new ZipPackage(downloadPath);

                    var shouldBeIncluded = nugetPackage.Tags != null && nugetPackage.Tags.ToLowerInvariant().Contains("aspnetwebpages");

                    if (!shouldBeIncluded)
                    {
                        shouldBeIncluded = true;
                        foreach (var file in nugetPackage.GetFiles())
                        {
                            var fi = new FileInfo(file.Path);
                            if (fi.Extension == ".ps1" || fi.Extension == ".t4")
                            {
                                shouldBeIncluded = false;
                                break;
                            }
                        }
                    }

                    if (shouldBeIncluded)
                    {
                        AddPackageToCuratedFeed(package);
                    }

                    File.Delete(downloadPath);

                    Interlocked.Increment(ref processedCount);
                    Log.Info(
                        "{2} package '{0}.{1}' ({3} of {4}).",
                        package.Id,
                        package.Version,
                        shouldBeIncluded ? "Curated" : "Ignored",
                        processedCount,
                        totalCount);
                }
                catch(Exception ex)
                {
                    Interlocked.Increment(ref processedCount);
                    Log.Error(
                        "Error curating package '{0}.{1}' ({2} of {3}): {4}.",
                        package.Id,
                        package.Version,
                        processedCount,
                        totalCount,
                        ex.Message);
                }
            });
        }

        string DownloadPackage(Package package)
        {
            var cloudClient = CreateBlobClient();

            var packagesBlobContainer = Util.GetPackagesBlobContainer(cloudClient);

            var packageFileName = Util.GetPackageFileName(package.Id, package.Version);

            var downloadPath = Path.Combine(_tempFolder, packageFileName);

            var blob = packagesBlobContainer.GetBlockBlobReference(packageFileName);
            blob.DownloadToFile(downloadPath);

            return downloadPath;
        }

        IEnumerable<string> GetAlreadyCuratedPackageIds()
        {
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();
                return dbExecutor.Query<string>(@"
                    SELECT pr.Id
                    FROM CuratedPackages cp
                        JOIN CuratedFeeds cf ON cf.[Key] = cp.CuratedFeedKey
                        JOIN PackageRegistrations pr on pr.[Key] = cp.PackageRegistrationKey
                    WHERE cf.Name = @name", new { name = "webmatrix" });
            }
        }

        IDictionary<string, Package> GetLatestStablePackages()
        {
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();
                var packages = dbExecutor.Query<Package>(@"
                    SELECT pr.Id, p.Version, p.Hash 
                    FROM Packages p
                        JOIN PackageRegistrations pr on pr.[Key] = p.PackageRegistrationKey
                    WHERE p.IsLatestStable = 1");
                return packages.ToDictionary(p => p.Id);
            }
        }

        void AddPackageToCuratedFeed(Package package)
        {
            if (!WhatIf)
            {
                using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();
                    dbExecutor.Execute(@"
                    INSERT INTO CuratedPackages 
                        (CuratedFeedKey, PackageRegistrationKey, AutomaticallyCurated, Included)
                    VALUES
                        ((SELECT [Key] FROM CuratedFeeds WHERE Name = 'webmatrix'), (SELECT [Key] FROM PackageRegistrations WHERE Id = @id), @automaticallyCurated, @included)",
                        new { id = package.Id, automaticallyCurated = true, included = true });
                }
            }
        }
    }
}
