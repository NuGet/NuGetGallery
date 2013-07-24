using AnglicanGeek.DbExecutor;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks
{
    [Command("updatelicensereports", "Updates the license reports from SonaType", AltName="ulr")]
    public class UpdateLicenseReportsTask : DatabaseTask
    {
        private class PackageLicenseReport
        {
            public int Sequence { set; get; }
            public string PackageId { set; get; }
            public string Version { set; get; }
            public string ReportUrl { set; get; }
            public string Comment { set; get; }
            public ICollection<string> Licenses { private set; get; }

            public PackageLicenseReport(int sequence)
            {
                this.Sequence = sequence;
                this.PackageId = null;
                this.Version = null;
                this.ReportUrl = null;
                this.Comment = null;
                this.Licenses = new LinkedList<string>();
            }

            public override string ToString()
            {
                return "{ " + Sequence.ToString() + ", "
                    + String.Join(", ", new string[] { PackageId, Version, ReportUrl, Comment })
                    + ", [ " + String.Join(", ", Licenses) + " ] }";
            }
        }

        private static class PackageLicenseReportsStorage
        {
            public static void Store(PackageLicenseReport report, SqlConnection connection)
            {
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "AddPackageLicenseReport";
                    command.CommandType = CommandType.StoredProcedure;

                    DataTable licensesNames = new DataTable();
                    licensesNames.Columns.Add("Name", typeof(string));
                    foreach (string license in report.Licenses)
                    {
                        licensesNames.Rows.Add(license);
                    }
                    command.Parameters.AddWithValue("@licensesNames", licensesNames);

                    command.Parameters.AddWithValue("@sequence", report.Sequence);
                    command.Parameters.AddWithValue("@packageId", report.PackageId);
                    command.Parameters.AddWithValue("@version", report.Version);
                    command.Parameters.AddWithValue("@reportUrl", report.ReportUrl);
                    command.Parameters.AddWithValue("@comment", report.Comment);

                    command.ExecuteNonQuery();
                }
            }
        }

        private static readonly JsonSchema sonatypeSchema = JsonSchema.Parse(@"{ 'type': 'object', 'properties': {
                'next'   : { 'type' : 'string' },
                'events' : { 'type': 'array', 'items': { 'type': 'object', 'properties': {                                
                        'sequence'  : { 'type' : 'integer' },
                        'packageId' : { 'type' : 'string' },
                        'version'   : { 'type' : 'string' },
                        'licenses'  : { 'type' : 'array', 'items': { 'type': 'string' } },
                        'reportUrl' : { 'type' : 'string' },
                        'comment'   : { 'type' : 'string' } } } } } }");
  
        private static PackageLicenseReport CreateReport(JObject messageEvent)
        {
            PackageLicenseReport report = new PackageLicenseReport(messageEvent["sequence"].Value<int>());
            report.PackageId = messageEvent["packageId"].Value<string>();
            report.Version = messageEvent["version"].Value<string>();
            report.ReportUrl = messageEvent["reportUrl"].Value<string>();
            report.Comment = messageEvent["comment"].Value<string>();
            foreach (JValue l in messageEvent["licenses"])
            {
                report.Licenses.Add(l.Value<string>());
            }
            return report;
        }

        public override void ExecuteCommand()
        {
            Log.Info("Loading URL for the next license report");
            string nextLicenseReport = null;
            try
            {
                WithConnection((connection, executor) =>
                {
                    nextLicenseReport = executor.Query<string>(
                        @"SELECT NextLicenseReport FROM GallerySettings").FirstOrDefault();
                });
            }
            catch (Exception e)
            {
                Log.Error("Database error\n\nCallstack:\n" + e.ToString());
                return;
            }
            if (nextLicenseReport == null)
            {
                Log.Error("URL for the next license report was not found");
                return;
            }
            Log.Info("Found URL for the next license report: {0}", nextLicenseReport);

            Boolean hasNext = true;
            while (hasNext)
            {
                hasNext = false;

                Log.Info("Sending request to {0}", nextLicenseReport);
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(nextLicenseReport);

                Log.Info("Receiving response");
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Log.Info("Parsing JSON from response");
                    string content = (new StreamReader(response.GetResponseStream())).ReadToEnd();
                    JObject sonatypeMessage = JObject.Parse(content);
                    if (!sonatypeMessage.IsValid(sonatypeSchema))
                    {
                        Log.Error("JSON message uses an invalid schema");
                        return;
                    }

                    foreach (JObject messageEvent in sonatypeMessage["events"])
                    {
                        PackageLicenseReport report = CreateReport(messageEvent);
                        
                        Log.Info("Found new report for package {0} {1}", report.PackageId, report.Version);
                        if (!WhatIf)
                        {
                            Log.Info("Saving new report for package {0} {1}", report.PackageId, report.Version);
                            try
                            {
                                WithConnection((connection) =>
                                {
                                    PackageLicenseReportsStorage.Store(report, connection);
                                });
                            }
                            catch (Exception e)
                            {
                                Log.Error("Database error\n\nCallstack:\n{0}", e.ToString());
                                return;
                            }
                        }
                    }

                    if (sonatypeMessage["next"].Value<string>().Length > 0)
                    {
                        hasNext = true;
                        nextLicenseReport = sonatypeMessage["next"].Value<string>();
                        Log.Info("Found URL for the next license report: {0}", nextLicenseReport);
                        if (!WhatIf)
                        {
                            Log.Info("Saving URL for the next license report: {0}", nextLicenseReport);
                            try
                            {
                                WithConnection((connection, executor) =>
                                {
                                    executor.Execute(@"
                                    UPDATE GallerySettings
                                    SET NextLicenseReport = @nextLicenseReport",
                                        new { nextLicenseReport });
                                });
                            }
                            catch (Exception e)
                            {
                                Log.Error("Database error\n\nCallstack:\n{0}", e.ToString());
                                return;
                            }
                        }
                    }
                }
                else if (response.StatusCode != HttpStatusCode.NoContent)
                {
                    Log.Info("Report is not available");
                }
                else
                {
                    Log.Error("URL for the next license report caused HTTP status {0}", response.StatusCode);
                    return;
                }
            }
        }
    }
}
