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
                    + String.Join(", ", new string[] { PackageId, PackageId, Version, ReportUrl, Comment })
                    + ", [ " + String.Join(", ", Licenses) + " ] }";
            }
        }

        private class PackageLicenseReportsStorage
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
            
            string nextLicenseReport = null;
            WithConnection((connection, executor) =>
            {
                nextLicenseReport = executor.Query<string>(
                    @"SELECT NextLicenseReport FROM GallerySettings").FirstOrDefault();
            });
 
            Boolean hasNext = true;
            while (hasNext)
            {
                hasNext = false;

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(nextLicenseReport);
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
 
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = (new StreamReader(response.GetResponseStream())).ReadToEnd();
                    JObject sonatypeMessage = JObject.Parse(content);

                    if (!sonatypeMessage.IsValid(sonatypeSchema))
                    {
                        if (!WhatIf)
                        {
                            // TODO: Report to backend log.
                        }
                        return;
                    }

                    foreach (JObject messageEvent in sonatypeMessage["events"])
                    {
                        PackageLicenseReport report = CreateReport(messageEvent);
                        if (!WhatIf)
                        {
                            WithConnection((connection) =>
                            {
                                PackageLicenseReportsStorage.Store(report, connection);
                            });
                        }
                    }

                    if (sonatypeMessage["next"].Value<string>().Count() > 0)
                    {
                        hasNext = true;
                        nextLicenseReport = sonatypeMessage["next"].Value<string>();
                        if (!WhatIf)
                        {
                            WithConnection((connection, executor) =>
                            {
                                executor.Execute(@"
                                    UPDATE GallerySettings
                                    SET NextLicenseReport = @nextLicenseReport",
                                    new { nextLicenseReport });
                            });
                        }
                    }
                }
                else if (response.StatusCode != HttpStatusCode.NoContent)
                {
                    if (!WhatIf)
                    {
                        // TODO: Report to backend log.
                    }
                    return;
                }
            }
        }
    }
}
