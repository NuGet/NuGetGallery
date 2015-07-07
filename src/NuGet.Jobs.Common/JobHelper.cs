using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace NuGet.Jobs
{
    public static class JobHelper
    {
        public static JArray MakeJArray(IDictionary<string, HashSet<string>> data)
        {
            JArray result = new JArray();
            foreach (var entry in data)
            {
                result.Add(new JArray(entry.Key, new JArray(entry.Value.ToArray())));
            }
            return result;
        }

        public static JArray SqlDataReader2Json(SqlDataReader reader, string col0, string col1)
        {
            var colNames = new Dictionary<string, int>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                colNames[reader.GetName(i)] = i;
            }

            var parent = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            while (reader.Read())
            {
                string parentColumn = reader.GetString(colNames[col0]);
                string childColumn = reader.GetString(colNames[col1]);

                HashSet<string> child;
                if (!parent.TryGetValue(parentColumn, out child))
                {
                    child = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    parent.Add(parentColumn, child);
                }

                child.Add(childColumn);
            }

            return MakeJArray(parent);
        }

        public static string LoadResource(Assembly assembly, string resourceName)
        {
            string name = assembly.GetName().Name;
            Stream stream = assembly.GetManifestResourceStream(name + "." + resourceName);
            return new StreamReader(stream).ReadToEnd();
        }

        public static async Task WriteReport(SqlExportArguments args, string content)
        {
            if (!String.IsNullOrEmpty(args.OutputDirectory))
            {
                await WriteToFile(args.OutputDirectory, content, args.Name);
            }
            else
            {
                await WriteToBlob(args.DestinationContainer, content, args.Name);
            }
        }

        public static async Task WriteToFile(string outputDirectory, string content, string name)
        {
            string fullPath = Path.Combine(outputDirectory, name);
            string parentDir = Path.GetDirectoryName(fullPath);
            Trace.TraceInformation(String.Format("Writing report to {0}", fullPath));

            if (!Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            using (var writer = new StreamWriter(File.OpenWrite(fullPath)))
            {
                await writer.WriteAsync(content);
            }

            Trace.TraceInformation(String.Format("Wrote report to {0}", fullPath));
        }

        public static async Task WriteToBlob(CloudBlobContainer container, string content, string name)
        {
            await container.CreateIfNotExistsAsync();

            var blob = container.GetBlockBlobReference(name);
            Trace.TraceInformation(String.Format("Writing report to {0}", blob.Uri.AbsoluteUri));

            blob.Properties.ContentType = "application/json";
            await blob.UploadTextAsync(content);

            Trace.TraceInformation(String.Format("Wrote report to {0}", blob.Uri.AbsoluteUri));
        }

        public static void TraceSqlExportArguments(SqlExportArguments args)
        {
            Trace.TraceInformation(String.Format("Generating Curated feed report from {0}.", TracableConnectionString(args.ConnectionString)));
        }

        public static string TracableConnectionString(string connectionString)
        {
            SqlConnectionStringBuilder connStr = new SqlConnectionStringBuilder(connectionString);
            connStr.UserID = "########";
            connStr.Password = "########";
            return connStr.ToString();
        }

        public static async Task<bool> RunSqlExport(SqlExportArguments args, string sql, string col0, string col1)
        {
            JobHelper.TraceSqlExportArguments(args);

            using (SqlConnection connection = new SqlConnection(args.ConnectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand(sql, connection);
                command.CommandType = CommandType.Text;
                JArray result = JobHelper.SqlDataReader2Json(command.ExecuteReader(), col0, col1);
                await JobHelper.WriteReport(args, result.ToString(Formatting.None));
            }

            return true;
        }
    }
}
