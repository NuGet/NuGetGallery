// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AnglicanGeek.DbExecutor;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using NuGet;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("populatepackageframeworks", "Populate the Package Frameworks index in the database from the data in the storage server", AltName = "pfx", IsSpecialPurpose = true)]
    public class PopulatePackageFrameworksTask : DatabaseAndStorageTask
    {
        private static readonly int _padLength = Enum.GetValues(typeof(PackageReportState)).Cast<PackageReportState>().Select(p => p.ToString().Length).Max();
        private static readonly JsonSerializer _serializer = new JsonSerializer()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            TypeNameHandling = TypeNameHandling.None,
            Formatting = Formatting.Indented,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
        };

        private readonly string _tempFolder;

        [Option("directory in which to put resume data and other work", AltName = "w")]
        public string WorkDirectory { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            ArgCheck.Required(WorkDirectory, "WorkDirectory");
        }

        public PopulatePackageFrameworksTask()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "NuGetGalleryOps");
            Directory.CreateDirectory(_tempFolder);
        }

        public override void ExecuteCommand()
        {
            if (!Directory.Exists(WorkDirectory))
            {
                Directory.CreateDirectory(WorkDirectory);
            }

            Log.Info("Getting all package metadata...");
            var packages = GetAllPackages();

            var totalCount = packages.Count;
            var processedCount = 0;
            Log.Info(
                "Populating frameworks for {0} packages on '{1}',",
                totalCount,
                ConnectionString);


            packages
                .AsParallel()
                .AsOrdered()
                .WithDegreeOfParallelism(10)
                .ForAll(package =>
            {
                // Allocate a processed count number for this package
                var thisPackageId = Interlocked.Increment(ref processedCount);
                
                try
                {
                    var reportPath = Path.Combine(WorkDirectory, package.Id + "_" + package.Version + ".json");
                    var bustedReportPath = Path.Combine(WorkDirectory, package.Id + "_" + package.Version + "_" + package.Hash + ".json");

                    var report = new PackageFrameworkReport()
                    {
                        Id = package.Id,
                        Version = package.Version,
                        Key = package.Key,
                        Hash = package.Hash,
                        Created = package.Created.Value,
                        State = PackageReportState.Unresolved
                    };

                    if (File.Exists(bustedReportPath))
                    {
                        File.Move(bustedReportPath, reportPath);
                    }

                    if (File.Exists(reportPath))
                    {
                        using (var reader = File.OpenText(reportPath))
                        {
                            report = (PackageFrameworkReport)_serializer.Deserialize(reader, typeof(PackageFrameworkReport));
                        }
                        ResolveReport(report);
                    }
                    else
                    {
                        try
                        {
                            var downloadPath = DownloadPackage(package);
                            var nugetPackage = new ZipPackage(downloadPath);

                            var supportedFrameworks = GetSupportedFrameworks(nugetPackage);
                            report.PackageFrameworks = supportedFrameworks.ToArray();
                            report = PopulateFrameworks(package, report);

                            File.Delete(downloadPath);

                            // Resolve the report
                            ResolveReport(report);
                        }
                        catch (Exception ex)
                        {
                            report.State = PackageReportState.Error;
                            report.Error = ex.ToString();
                        }
                    }

                    using (var writer = File.CreateText(reportPath))
                    {
                        _serializer.Serialize(writer, report);
                    }

                    Log.Info("[{2}/{3} {4}%] {6} Package: {0}@{1} (created {5})",
                        package.Id,
                        package.Version,
                        thisPackageId.ToString("000000"),
                        totalCount.ToString("000000"),
                        (((double)thisPackageId / (double)totalCount) * 100).ToString("000.00"),
                        package.Created.Value,
                        report.State.ToString().PadRight(_padLength, ' '));
                }
                catch (Exception ex)
                {
                    Log.Error("[{2}/{3} {4}%] Error For Package: {0}@{1}: {5}",
                        package.Id,
                        package.Version,
                        thisPackageId.ToString("000000"),
                        totalCount.ToString("000000"),
                        (((double)thisPackageId / (double)totalCount) * 100).ToString("000.00"),
                        ex.ToString());
                }
            });
        }

        private void ResolveReport(PackageFrameworkReport report)
        {
            bool error = false;
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();
                foreach (var operation in report.Operations)
                {
                    if (!WhatIf)
                    {
                        if (operation.Type == PackageFrameworkOperationType.Add)
                        {
                            try
                            {
                                dbExecutor.Execute(@"
                                    INSERT INTO PackageFrameworks(TargetFramework, Package_Key)
                                    VALUES (@targetFramework, @packageKey)",
                                        new
                                        {
                                            targetFramework = operation.Framework,
                                            packageKey = report.Key
                                        });
                                Log.Info(" + Id={0}, Key={1}, Fx={2}", report.Id, report.Key, operation.Framework);
                                operation.Applied = true;
                            }
                            catch (Exception ex)
                            {
                                error = true;
                                operation.Applied = false;
                                operation.Error = ex.ToString();
                            }
                        }
                        else if (operation.Type == PackageFrameworkOperationType.Remove)
                        {
                            try
                            {
                                dbExecutor.Execute(@"
                                    DELETE FROM PackageFrameworks
                                    WHERE TargetFramework = @targetFramework AND Package_Key = @packageKey",
                                        new
                                        {
                                            targetFramework = operation.Framework,
                                            packageKey = report.Key
                                        });
                                Log.Info(" - Id={0}, Key={1}, Fx={2}", report.Id, report.Key, operation.Framework);
                                operation.Applied = true;
                            }
                            catch (Exception ex)
                            {
                                error = true;
                                operation.Applied = false;
                                operation.Error = ex.ToString();
                            }
                        }
                    }
                }
            }

            if (error)
            {
                report.State = PackageReportState.Error;
            }
            else if (report.Operations.All(o => o.Applied))
            {
                report.State = PackageReportState.Resolved;
            }
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

        IList<Package> GetAllPackages()
        {
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();
                var packages = dbExecutor.Query<Package>(@"
                    SELECT p.[Key], pr.Id, p.Version, p.Hash, p.Created
                    FROM Packages p
                        JOIN PackageRegistrations pr on pr.[Key] = p.PackageRegistrationKey
                    ORDER BY p.Created DESC");
                return packages.ToList();
            }
        }

        PackageFrameworkReport PopulateFrameworks(
            Package package,
            PackageFrameworkReport report)
        {
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();

                // Get all target frameworks in the db for this package
                report.DatabaseFrameworks = new HashSet<string>(dbExecutor.Query<string>(@"
                    SELECT TargetFramework
                    FROM PackageFrameworks
                    WHERE Package_Key = @packageKey",
                    new
                    {
                        packageKey = package.Key
                    })).ToArray();

                var adds = report.PackageFrameworks.Except(report.DatabaseFrameworks).Select(targetFramework =>
                    new PackageFrameworkOperation()
                    {
                        Type = PackageFrameworkOperationType.Add,
                        Framework = targetFramework,
                        Applied = false,
                        Error = "Not Started"
                    });
                var rems = report.DatabaseFrameworks.Except(report.PackageFrameworks).Select(targetFramework =>
                    new PackageFrameworkOperation()
                    {
                        Type = PackageFrameworkOperationType.Remove,
                        Framework = targetFramework,
                        Applied = false,
                        Error = "Not Started"
                    });

                report.Operations = Enumerable.Concat(adds, rems).ToArray();
            }

            return report;
        }

        private static IEnumerable<string> GetSupportedFrameworks(IPackage nugetPackage)
        {
            return nugetPackage.GetSupportedFrameworks().Select(fn => fn.ToShortNameOrNull()).ToArray();
        }

        public class PackageFrameworkReport
        {
            public string Id { get; set; }
            public string Version { get; set; }
            public int Key { get; set; }
            public string Hash { get; set; }
            public DateTime Created { get; set; }
            public string[] DatabaseFrameworks { get; set; }
            public string[] PackageFrameworks { get; set; }
            public PackageFrameworkOperation[] Operations { get; set; }
            public string Error { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            public PackageReportState State { get; set; }
        }

        public class PackageFrameworkOperation
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public PackageFrameworkOperationType Type { get; set; }
            public string Framework { get; set; }
            public bool Applied { get; set; }
            public string Error { get; set; }
        }

        public enum PackageFrameworkOperationType
        {
            Add,
            Remove
        }

        public enum PackageReportState {
            Unresolved,
            Resolved,
            Error
        }
    }
}
