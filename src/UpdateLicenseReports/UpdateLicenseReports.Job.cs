using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using NuGet.Jobs;

namespace UpdateLicenseReports
{
    internal class Job : JobBase
    {
        public static readonly int DefaultRetryCount = 4;

        private static readonly JsonSchema sonatypeSchema = JsonSchema.Parse(@"{ 'type': 'object',
            'properties': {
                'next'   : { 'type' : 'string' },
                'events' : {
                    'type': 'array',
                    'items': {
                        'type': 'object',
                        'properties': {
                            'sequence'  : { 'type' : 'integer', 'required': true },
                            'packageId' : { 'type' : 'string', 'required': true },
                            'version'   : { 'type' : 'string', 'required': true },
                            'licenses'  : { 'type' : 'array', 'items': { 'type': 'string' } },
                            'reportUrl' : { 'type' : 'string' },
                            'comment'   : { 'type' : 'string' }
                        } } } } }");

        /// <summary>
        /// Gets or sets the base url for the license report service
        /// </summary>
        public Uri LicenseReportService { get; set; }

        /// <summary>
        /// Gets or sets the username for the license report service
        /// </summary>
        public string LicenseReportUser { get; set; }

        /// <summary>
        /// Gets or sets the password for the license report service
        /// </summary>
        public string LicenseReportPassword { get; set; }

        /// <summary>
        /// Gets or sets a connection string to the database containing package data.
        /// </summary>
        public SqlConnectionStringBuilder PackageDatabase { get; set; }

        /// <summary>
        /// Gets or sets the number of times to retry each HTTP request.
        /// </summary>
        public int? RetryCount { get; set; }

        protected NetworkCredential LicenseReportCredentials { get; set; }

        private static PackageLicenseReport CreateReport(JObject messageEvent)
        {
            PackageLicenseReport report = new PackageLicenseReport(messageEvent["sequence"].Value<int>());
            report.PackageId = messageEvent.Value<string>("packageId");
            report.Version = messageEvent.Value<string>("version");
            report.ReportUrl = messageEvent.Value<string>("reportUrl");
            report.Comment = messageEvent.Value<string>("comment");
            foreach (JValue l in messageEvent["licenses"])
            {
                report.Licenses.Add(l.Value<string>());
            }
            return report;
        }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            PackageDatabase = new SqlConnectionStringBuilder(
                        JobConfigurationManager.GetArgument(jobArgsDictionary,
                            JobArgumentNames.PackageDatabase,
                            EnvironmentVariableKeys.SqlGallery));

            string retryCountString = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.RetryCount);
            if (string.IsNullOrEmpty(retryCountString))
            {
                RetryCount = DefaultRetryCount;
            }
            else
            {
                RetryCount = Convert.ToInt32(retryCountString);
            }

            LicenseReportService = new Uri(JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.LicenseReportService));
            LicenseReportUser = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.LicenseReportUser);
            LicenseReportPassword = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.LicenseReportPassword);


            // Build credentials
            if (!string.IsNullOrEmpty(LicenseReportUser))
            {
                if (!string.IsNullOrEmpty(LicenseReportPassword))
                {
                    LicenseReportCredentials = new NetworkCredential(LicenseReportUser, LicenseReportPassword);
                }
                else
                {
                    LicenseReportCredentials = new NetworkCredential(LicenseReportUser, string.Empty);
                }
            }
            else if (!string.IsNullOrEmpty(LicenseReportPassword))
            {
                LicenseReportCredentials = new NetworkCredential(string.Empty, LicenseReportPassword);
            }
            return true;

        }

        public override async Task<bool> Run()
        {
            // Fetch next report url
            Uri nextLicenseReport = await FetchNextReportUrl();

            // Process that report
            while (nextLicenseReport != null && await ProcessReports(nextLicenseReport))
            {
                nextLicenseReport = await FetchNextReportUrl();
            }
            return true;
        }

        private async Task<Uri> FetchNextReportUrl()
        {
            Trace.TraceInformation(string.Format("Fetching next report URL from {0}/{1}", PackageDatabase.DataSource, PackageDatabase.InitialCatalog));
            Uri nextLicenseReport = null;
            using (var connection = await PackageDatabase.ConnectTo())
            {
                var nextReportUrl = (await connection.QueryAsync<string>(
                    @"SELECT TOP 1 NextLicenseReport FROM GallerySettings")).SingleOrDefault();
                if (string.IsNullOrEmpty(nextReportUrl))
                {
                    Trace.TraceInformation("No next report URL found, using default");
                }
                else if (!Uri.TryCreate(nextReportUrl, UriKind.Absolute, out nextLicenseReport))
                {
                    Trace.TraceInformation(string.Format("Next Report URL '{0}' is invalid. Using default", nextReportUrl));
                }
                nextLicenseReport = nextLicenseReport ?? LicenseReportService;
            }
            Trace.TraceInformation(string.Format("Fetched next report URL '{2}' from {0}/{1}", PackageDatabase.DataSource, PackageDatabase.InitialCatalog, (nextLicenseReport == null ? string.Empty : nextLicenseReport.AbsoluteUri)));
            return nextLicenseReport;
        }

        private async Task<bool> ProcessReports(Uri nextLicenseReport)
        {
            HttpWebResponse response = null;
            int tries = 0;
            Trace.TraceInformation(string.Format("Downloading license report {0}", nextLicenseReport.AbsoluteUri));
            while (tries < RetryCount.Value && response == null)
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(nextLicenseReport);
                if (LicenseReportCredentials != null)
                {
                    request.Credentials = LicenseReportCredentials;
                }

                WebException thrown = null;
                try
                {
                    response = (HttpWebResponse)(await request.GetResponseAsync());
                }
                catch (WebException ex)
                {
                    response = null;
                    if (ex.Status == WebExceptionStatus.Timeout || ex.Status == WebExceptionStatus.ConnectFailure)
                    {
                        // Try again in 10 seconds
                        tries++;
                        if (tries < RetryCount.Value)
                        {
                            thrown = ex;
                        }
                        else
                        {
                            throw;
                        }
                    }
                    else
                    {
                        throw;
                    }
                }
                if (thrown != null)
                {
                    Trace.TraceInformation(string.Format("Error downloading report {0}, retrying. {1}", nextLicenseReport.AbsoluteUri, thrown.ToString()));
                    await Task.Delay(10 * 1000);
                }
            }
            Trace.TraceInformation(string.Format("Downloaded license report {0}", nextLicenseReport.AbsoluteUri));

            Trace.TraceInformation(string.Format("Processing license report {0}", nextLicenseReport.AbsoluteUri));
            using (response)
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Trace.TraceInformation(string.Format("Reading license report {0}", nextLicenseReport.AbsoluteUri));
                    string content;
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        content = await reader.ReadToEndAsync();
                    }
                    Trace.TraceInformation(string.Format("Read license report {0}", nextLicenseReport.AbsoluteUri));

                    JObject sonatypeMessage = JObject.Parse(content);
                    if (!sonatypeMessage.IsValid(sonatypeSchema))
                    {
                        Trace.TraceInformation(string.Format("Invalid license report in {0}. {1}", nextLicenseReport.AbsoluteUri, Strings.UpdateLicenseReportsJob_JsonDoesNotMatchSchema));
                        return false;
                    }

                    var events = sonatypeMessage["events"].Cast<JObject>().ToList();
                    for (int i = 0; i < events.Count; i++)
                    {
                        var messageEvent = events[i];
                        PackageLicenseReport report = CreateReport(messageEvent);
                        Trace.TraceInformation(string.Format("Storing license report for {0} {1}", report.PackageId, report.Version));

                        if (await StoreReport(report) == -1)
                        {
                            Trace.TraceInformation(string.Format("Unable to store report for {0} {1}. Package does not exist in database.", report.PackageId, report.Version));
                        }
                        else
                        {
                            Trace.TraceInformation(string.Format("Stored license report for {0} {1}", report.PackageId, report.Version));
                        }
                    }

                    // Store the next URL
                    if (sonatypeMessage["next"].Value<string>().Length > 0)
                    {
                        var nextReportUrl = sonatypeMessage["next"].Value<string>();
                        if (!Uri.TryCreate(nextReportUrl, UriKind.Absolute, out nextLicenseReport))
                        {
                            Trace.TraceInformation(string.Format("Invalid next report URL: {0}", nextReportUrl));
                            return false;
                        }
                        Trace.TraceInformation(string.Format("Storing next license report URL: {0}", nextLicenseReport.AbsoluteUri));

                        // Record the next report to the database so we can check it again if we get aborted before finishing.

                        using (var connection = await PackageDatabase.ConnectTo())
                        {
                            await connection.QueryAsync<int>(@"
                                        UPDATE GallerySettings
                                        SET NextLicenseReport = @nextLicenseReport",
                                new { nextLicenseReport = nextLicenseReport.AbsoluteUri });
                        }
                        return true; // Continue and read the next report later

                    }
                    else
                    {
                        nextLicenseReport = null;
                    }
                    Trace.TraceInformation(string.Format("Processing license report {0}", nextLicenseReport.AbsoluteUri));
                }
                else if (response.StatusCode != HttpStatusCode.NoContent)
                {
                    Trace.TraceInformation(string.Format("No report for {0} yet.", nextLicenseReport.AbsoluteUri));
                }
                else
                {
                    Trace.TraceInformation(string.Format("HTTP {1} error requesting {0}: {2}", nextLicenseReport.AbsoluteUri, (int)response.StatusCode, response.StatusDescription));
                }
                return false;
            }
        }

        private async Task<int> StoreReport(PackageLicenseReport report)
        {
            using (var connection = await PackageDatabase.ConnectTo())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "AddPackageLicenseReport";
                command.CommandType = CommandType.StoredProcedure;

                DataTable licensesNames = new DataTable();
                licensesNames.Columns.Add("Name", typeof(string));
                foreach (string license in report.Licenses.Select(l => l.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    licensesNames.Rows.Add(license);
                }
                command.Parameters.AddWithValue("@licenseNames", licensesNames);

                command.Parameters.AddWithValue("@sequence", report.Sequence);
                command.Parameters.AddWithValue("@packageId", report.PackageId);
                command.Parameters.AddWithValue("@version", report.Version);
                command.Parameters.AddWithValue("@reportUrl", report.ReportUrl ?? string.Empty);
                command.Parameters.AddWithValue("@comment", report.Comment);
                
                return (int)(await command.ExecuteScalarAsync());
            }
        }

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
                    + string.Join(", ", new string[] { PackageId, Version, ReportUrl, Comment })
                    + ", [ " + string.Join(", ", Licenses) + " ] }";
            }
        }

    }
}
