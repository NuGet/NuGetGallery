// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Documents;
using Lucene.Net.Store;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Indexing
{
    public class Sql2Lucene
    {
        static Document CreateDocument(SqlDataReader reader, IDictionary<int, List<string>> packageFrameworks)
        {
            var package = new Dictionary<string, string>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                if (!reader.IsDBNull(i))
                {
                    string name = reader.GetName(i);
                    object obj = reader.GetValue(i);

                    if (name == "key")
                    {
                        var key = (int)obj;
                        List<string> targetFrameworks;
                        if (packageFrameworks.TryGetValue(key, out targetFrameworks))
                        {
                            package.Add("supportedFrameworks", string.Join("|", targetFrameworks));
                        }
                    }

                    var value = (obj is DateTime) ? ((DateTime)obj).ToUniversalTime().ToString("O") : obj.ToString();

                    package.Add(name, value);
                }
            }

            return DocumentCreator.CreateDocument(package);
        }

        static string IndexBatch(string path, string connectionString, IDictionary<int, List<string>> packageFrameworks, int beginKey, int endKey)
        {
            var folder = string.Format(@"{0}\index_{1}_{2}", path, beginKey, endKey);

            var directoryInfo = new DirectoryInfo(folder);
            directoryInfo.Create();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                var cmdText = @"
                    SELECT
                        Packages.[Key]                          'key',
                        PackageRegistrations.Id                 'id',
                        Packages.[Version]                      'originalVersion',
                        Packages.NormalizedVersion              'version',
                        Packages.Title                          'title',
                        Packages.Tags                           'tags',
                        Packages.[Description]                  'description',
                        Packages.FlattenedAuthors               'authors',
                        Packages.Summary                        'summary',
                        Packages.IconUrl                        'iconUrl',
                        Packages.ProjectUrl                     'projectUrl',
                        Packages.MinClientVersion               'minClientVersion',
                        Packages.ReleaseNotes                   'releaseNotes',
                        Packages.Copyright                      'copyright',
                        Packages.[Language]                     'language',
                        Packages.LicenseUrl                     'licenseUrl',
                        Packages.RequiresLicenseAcceptance      'requireLicenseAcceptance',
                        Packages.[Hash]                         'packageHash',
                        Packages.HashAlgorithm                  'packageHashAlgorithm',
                        Packages.PackageFileSize                'packageSize',
                        Packages.FlattenedDependencies          'flattenedDependencies',
                        Packages.Created                        'created',
                        Packages.LastEdited                     'lastEdited',
                        Packages.Published                      'published',
                        Packages.Listed                         'listed',
                        Packages.SemVerLevelKey                 'semVerLevelKey'
                    FROM Packages
                    INNER JOIN PackageRegistrations ON Packages.PackageRegistrationKey = PackageRegistrations.[Key]
                      AND Packages.[Key] >= @BeginKey
                      AND Packages.[Key] < @EndKey
                    WHERE Packages.Deleted = 0
                    ORDER BY Packages.[Key]
                ";

                var command = new SqlCommand(cmdText, connection);
                command.CommandTimeout = (int)TimeSpan.FromMinutes(15).TotalSeconds;
                command.Parameters.AddWithValue("BeginKey", beginKey);
                command.Parameters.AddWithValue("EndKey", endKey);

                var reader = command.ExecuteReader();

                var batch = 0;

                var directory = new SimpleFSDirectory(directoryInfo);

                using (var writer = DocumentCreator.CreateIndexWriter(directory, true))
                {
                    while (reader.Read())
                    {
                        var document = CreateDocument(reader, packageFrameworks);

                        writer.AddDocument(document);

                        if (batch++ == 1000)
                        {
                            writer.Commit();
                            batch = 0;
                        }
                    }

                    if (batch > 0)
                    {
                        writer.Commit();
                    }
                }
            }

            return folder;
        }

        static List<Tuple<int, int>> CalculateBatches(string connectionString)
        {
            var batches = new List<Tuple<int, int>>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string cmdText = @"
                    SELECT Packages.[Key]
                    FROM Packages
                    INNER JOIN PackageRegistrations ON Packages.PackageRegistrationKey = PackageRegistrations.[Key]
                    WHERE Packages.Deleted = 0
                    ORDER BY Packages.[Key]
                ";

                var command = new SqlCommand(cmdText, connection);
                command.CommandTimeout = (int)TimeSpan.FromMinutes(15).TotalSeconds;

                var reader = command.ExecuteReader();

                var list = new List<int>();

                while (reader.Read())
                {
                    list.Add(reader.GetInt32(0));
                }

                int batch = 0;

                int beginKey = list.First();
                int endKey = 0;

                foreach (int x in list)
                {
                    endKey = x;

                    if (batch++ == 50000)
                    {
                        batches.Add(Tuple.Create(beginKey, endKey));
                        batch = 0;
                        beginKey = endKey;
                    }
                }

                batches.Add(Tuple.Create(beginKey, endKey + 1));
            }

            return batches;
        }

        static IDictionary<int, List<string>> LoadPackageFrameworks(string connectionString)
        {
            var result = new Dictionary<int, List<string>>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                var cmdText = @"SELECT Package_Key, TargetFramework FROM PackageFrameworks";

                var command = new SqlCommand(cmdText, connection);
                command.CommandTimeout = (int)TimeSpan.FromMinutes(15).TotalSeconds;

                var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    if (reader.IsDBNull(0) || reader.IsDBNull(1))
                    {
                        continue;
                    }

                    int packageKey = reader.GetInt32(0);
                    string targetFramework = reader.GetString(1);

                    List<string> targetFrameworks;
                    if (!result.TryGetValue(packageKey, out targetFrameworks))
                    {
                        targetFrameworks = new List<string>();
                        result.Add(packageKey, targetFrameworks);
                    }

                    targetFrameworks.Add(targetFramework);
                }
            }

            return result;
        }

        public static void Export(string sourceConnectionString, string destinationPath, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger<Sql2Lucene>();
            var stopwatch = new Stopwatch();

            stopwatch.Start();

            var batches = CalculateBatches(sourceConnectionString);
            logger.LogInformation("Calculated {BatchCount} batches (took {BatchCalculationTime} seconds)", batches.Count, stopwatch.Elapsed.TotalSeconds);

            stopwatch.Restart();

            var packageFrameworks = LoadPackageFrameworks(sourceConnectionString);
            logger.LogInformation("Loaded package frameworks (took {PackageFrameworksLoadTime} seconds)", stopwatch.Elapsed.TotalSeconds);

            stopwatch.Restart();

            var tasks = new List<Task<string>>();
            foreach (var batch in batches)
            {
                tasks.Add(Task.Run(() => { return IndexBatch(destinationPath + @"\batches", sourceConnectionString, packageFrameworks, batch.Item1, batch.Item2); }));
            }

            try
            {
                Task.WaitAll(tasks.ToArray());
            }
            catch (AggregateException ex)
            {
                logger.LogError("An AggregateException occurred while running batches.", ex);

                throw;
            }

            logger.LogInformation("Partition indexes generated (took {PartitionIndexGenerationTime} seconds", stopwatch.Elapsed.TotalSeconds);

            stopwatch.Restart();

            using (var directory = new SimpleFSDirectory(new DirectoryInfo(destinationPath)))
            {
                using (var writer = DocumentCreator.CreateIndexWriter(directory, true))
                {
                    NuGetMergePolicyApplyer.ApplyTo(writer);

                    var partitions = tasks.Select(t => new SimpleFSDirectory(new DirectoryInfo(t.Result))).ToArray();
                    
                    writer.AddIndexesNoOptimize(partitions);

                    foreach (var partition in partitions)
                    {
                        partition.Dispose();
                    }

                    writer.Commit(DocumentCreator.CreateCommitMetadata(DateTime.UtcNow, "from SQL", writer.NumDocs(), Guid.NewGuid().ToString())
                        .ToDictionary());
                }
            }

            logger.LogInformation("Sql2Lucene.Export done (took {Sql2LuceneExportTime} seconds)", stopwatch.Elapsed.TotalSeconds);

            stopwatch.Reset();
        }
    }
}
