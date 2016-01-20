// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Indexing
{
    public static class Sql2Lucene
    {
        static Document CreateDocument(SqlDataReader reader, IDictionary<int, List<string>> packageFrameworks)
        {
            var package = new Dictionary<string, string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (!reader.IsDBNull(i))
                {
                    string name = reader.GetName(i);
                    object obj = reader.GetValue(i);

                    if (name == "key")
                    {
                        int key = (int)obj;
                        List<string> targetFrameworks;
                        if (packageFrameworks.TryGetValue(key, out targetFrameworks))
                        {
                            package.Add("supportedFrameworks", string.Join("|", targetFrameworks));
                        }
                    }

                    string value = (obj is DateTime) ? ((DateTime)obj).ToUniversalTime().ToString("O") : obj.ToString();

                    package.Add(name, value);
                }
            }
            return DocumentCreator.CreateDocument(package);
        }

        static string IndexBatch(string path, string connectionString, IDictionary<int, List<string>> packageFrameworks, int beginKey, int endKey)
        {
            string folder = string.Format(@"{0}\index_{1}_{2}", path, beginKey, endKey);

            DirectoryInfo directoryInfo = new DirectoryInfo(folder);
            directoryInfo.Create();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string cmdText = @"
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
                        Packages.RequiresLicenseAcceptance      'requiresLicenseAcceptance',
                        Packages.[Hash]                         'packageHash',
                        Packages.HashAlgorithm                  'packageHashAlgorithm',
                        Packages.PackageFileSize                'packageSize',
                        Packages.FlattenedDependencies          'flattenedDependencies',
                        Packages.Created                        'created',
                        Packages.LastEdited                     'lastEdited',
                        Packages.Published                      'published',
                        Packages.Listed                         'listed'
                    FROM Packages
                    INNER JOIN PackageRegistrations ON Packages.PackageRegistrationKey = PackageRegistrations.[Key]
                    WHERE Packages.Listed = 1
                      AND Packages.[Key] >= @BeginKey
                      AND Packages.[Key] < @EndKey
                    ORDER BY Packages.[Key]
                ";

                SqlCommand command = new SqlCommand(cmdText, connection);
                command.CommandTimeout = (int)TimeSpan.FromMinutes(15).TotalSeconds;
                command.Parameters.AddWithValue("BeginKey", beginKey);
                command.Parameters.AddWithValue("EndKey", endKey);

                SqlDataReader reader = command.ExecuteReader();

                int batch = 0;

                SimpleFSDirectory directory = new SimpleFSDirectory(directoryInfo);

                using (IndexWriter writer = DocumentCreator.CreateIndexWriter(directory, true))
                {
                    while (reader.Read())
                    {
                        Document document = CreateDocument(reader, packageFrameworks);

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
            List<Tuple<int, int>> batches = new List<Tuple<int, int>>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string cmdText = @"
                    SELECT Packages.[Key]
                    FROM Packages
                    INNER JOIN PackageRegistrations ON Packages.PackageRegistrationKey = PackageRegistrations.[Key]
                    WHERE Packages.Listed = 1
                    ORDER BY Packages.[Key]
                ";

                SqlCommand command = new SqlCommand(cmdText, connection);
                command.CommandTimeout = (int)TimeSpan.FromMinutes(15).TotalSeconds;

                SqlDataReader reader = command.ExecuteReader();

                List<int> l = new List<int>();

                while (reader.Read())
                {
                    l.Add(reader.GetInt32(0));
                }

                int batch = 0;

                int beginKey = l.First();
                int endKey = 0;

                foreach (int x in l)
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

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string cmdText = @"SELECT Package_Key, TargetFramework FROM PackageFrameworks";

                SqlCommand command = new SqlCommand(cmdText, connection);
                command.CommandTimeout = (int)TimeSpan.FromMinutes(15).TotalSeconds;

                SqlDataReader reader = command.ExecuteReader();

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

        public static void Export(string sourceConnectionString, string destinationPath, TextWriter log)
        {
            Stopwatch stopwatch = new Stopwatch();

            stopwatch.Start();

            List<Tuple<int, int>> batches = CalculateBatches(sourceConnectionString);

            log.WriteLine("Calculated batches - {0} batches (took {1} seconds)", batches.Count, stopwatch.Elapsed.TotalSeconds);

            stopwatch.Restart();

            IDictionary<int, List<string>> packageFrameworks = LoadPackageFrameworks(sourceConnectionString);

            log.WriteLine("Load PackageFrameworks (took {0} seconds)", stopwatch.Elapsed.TotalSeconds);

            stopwatch.Restart();

            List<Task<string>> tasks = new List<Task<string>>();

            foreach (Tuple<int, int> batch in batches)
            {
                tasks.Add(Task.Run(() => { return IndexBatch(destinationPath + @"\batches", sourceConnectionString, packageFrameworks, batch.Item1, batch.Item2); }));
            }

            try
            {
                Task.WaitAll(tasks.ToArray());
            }
            catch (AggregateException ex)
            {
                var flattenedAggregateException = ex.Flatten();

                log.WriteLine("An AggregateException occurred while running batches.");
                foreach (var innerException in flattenedAggregateException.InnerExceptions)
                {
                    log.WriteLine("Message: {0} - Stack trace: {1}", ex.Message, innerException.StackTrace);
                }

                throw;
            }

            log.WriteLine("Partition indexes generated (took {0} seconds)", stopwatch.Elapsed.TotalSeconds);

            stopwatch.Restart();

            using (var directory = new SimpleFSDirectory(new DirectoryInfo(destinationPath)))
            {
                using (IndexWriter writer = DocumentCreator.CreateIndexWriter(directory, true))
                {
                    Lucene.Net.Store.Directory[] partitions = tasks.Select(t => new SimpleFSDirectory(new DirectoryInfo(t.Result))).ToArray();

                    writer.AddIndexesNoOptimize(partitions);

                    foreach (var partition in partitions)
                    {
                        partition.Dispose();
                    }

                    writer.Commit(DocumentCreator.CreateCommitMetadata(DateTime.UtcNow, "from SQL", Guid.NewGuid().ToString()));
                }
            }

            log.WriteLine("All done (took {0} seconds)", stopwatch.Elapsed.TotalSeconds);

            stopwatch.Restart();
        }
    }
}
