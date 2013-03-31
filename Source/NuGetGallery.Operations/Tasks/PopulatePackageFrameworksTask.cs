using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnglicanGeek.DbExecutor;
using NuGet;

namespace NuGetGallery.Operations
{
    [Command("populatepackageframeworks", "Populate the Package Frameworks index in the database from the data in the storage server", AltName = "pfx", IsSpecialPurpose = true)]
    public class PopulatePackageFrameworksTask : DatabaseAndStorageTask
    {
        private readonly string _tempFolder;

        public PopulatePackageFrameworksTask()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "NuGetGalleryOps");
            Directory.CreateDirectory(_tempFolder);
        }

        public override void ExecuteCommand()
        {
            Log.Info("Getting all package metadata...");
            var packages = GetAllPackages();
            Log.Info("Getting packages which have target framework data...");
            var alreadyPopulatedPackageKeys = GetAlreadyPopulatedPackageKeys();
            Log.Info("Calculating minimal difference set...");
            var packageKeysToPopulate = packages.Keys.Except(alreadyPopulatedPackageKeys).ToList();

            var totalCount = packageKeysToPopulate.Count;
            var processedCount = 0;
            Log.Info(
                "Populating frameworks for {0} packages on '{1}',",
                totalCount,
                ConnectionString);

            Parallel.ForEach(packageKeysToPopulate, new ParallelOptions { MaxDegreeOfParallelism = 10 }, packageIdToPopulate =>
            {
                var package = packages[packageIdToPopulate];

                try
                {
                    var downloadPath = DownloadPackage(package);
                    var nugetPackage = new ZipPackage(downloadPath);

                    var supportedFrameworks = GetSupportedFrameworks(nugetPackage);
                    if (!WhatIf)
                    {
                        PopulateFrameworks(package, supportedFrameworks);
                    }

                    File.Delete(downloadPath);

                    Interlocked.Increment(ref processedCount);
                    Log.Info(
                        "Populated frameworks for package '{0}.{1}' ({2} of {3}).",
                        package.Id,
                        package.Version,
                        processedCount,
                        totalCount);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref processedCount);
                    Log.Error(
                        "Error populating frameworks for package '{0}.{1}' ({2} of {3}): {4}.",
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

        IEnumerable<int> GetAlreadyPopulatedPackageKeys()
        {
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();
                return dbExecutor.Query<int>(@"
                    SELECT pf.Package_Key
                    FROM PackageFrameworks pf");
            }
        }

        IDictionary<int, Package> GetAllPackages()
        {
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();
                var packages = dbExecutor.Query<Package>(@"
                    SELECT p.[Key], pr.Id, p.Version, p.Hash 
                    FROM Packages p
                        JOIN PackageRegistrations pr on pr.[Key] = p.PackageRegistrationKey");
                return packages.ToDictionary(p => p.Key);
            }
        }

        void PopulateFrameworks(
            Package package,
            IEnumerable<string> targetFrameworks)
        {
            foreach (var targetFramework in targetFrameworks)
            {
                using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();
                    dbExecutor.Execute(
                        @"
                    INSERT INTO PackageFrameworks 
                        (Package_Key, TargetFramework)
                    VALUES
                        (@packageKey, @targetFramework)",
                        new { packageKey = package.Key, targetFramework});
                }
            }
        }

        private static IEnumerable<string> GetSupportedFrameworks(IPackage nugetPackage)
        {
            var supportedFrameworks = nugetPackage.GetSupportedFrameworks().Select(fn => fn.ToShortNameOrNull()).ToArray();
            if (!supportedFrameworks.AnySafe(sf => sf == null))
                return new string[]{};
            return supportedFrameworks;
        }
    }
}
