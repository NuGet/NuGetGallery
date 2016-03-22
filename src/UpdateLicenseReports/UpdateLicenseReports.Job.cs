using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Dapper;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using NuGet.Jobs;

namespace UpdateLicenseReports
{
    internal class Job : JobBase
    {
        private const int _defaultRetryCount = 4;
        private static readonly JsonSchema _sonatypeSchema = JsonSchema.Parse(@"{ 'type': 'object',
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

        private Uri _licenseReportService;
        private string _licenseReportUser;
        private string _licenseReportPassword;
        private SqlConnectionStringBuilder _packageDatabase;
        private int? _retryCount;
        private NetworkCredential _licenseReportCredentials;

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
            _packageDatabase = new SqlConnectionStringBuilder(
                        JobConfigurationManager.GetArgument(jobArgsDictionary,
                            JobArgumentNames.PackageDatabase,
                            EnvironmentVariableKeys.SqlGallery));

            var retryCountString = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.RetryCount);
            if (string.IsNullOrEmpty(retryCountString))
            {
                _retryCount = _defaultRetryCount;
            }
            else
            {
                _retryCount = Convert.ToInt32(retryCountString);
            }

            _licenseReportService = new Uri(JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.LicenseReportService));
            _licenseReportUser = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.LicenseReportUser);
            _licenseReportPassword = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.LicenseReportPassword);

            // Build credentials
            if (!string.IsNullOrEmpty(_licenseReportUser))
            {
                if (!string.IsNullOrEmpty(_licenseReportPassword))
                {
                    _licenseReportCredentials = new NetworkCredential(_licenseReportUser, _licenseReportPassword);
                }
                else
                {
                    _licenseReportCredentials = new NetworkCredential(_licenseReportUser, string.Empty);
                }
            }
            else if (!string.IsNullOrEmpty(_licenseReportPassword))
            {
                _licenseReportCredentials = new NetworkCredential(string.Empty, _licenseReportPassword);
            }

            return true;
        }

        public override async Task<bool> Run()
        {
            // Fetch next report url
            var nextLicenseReport = await FetchNextReportUrlAsync();

            // Process that report
            while (nextLicenseReport != null && await ProcessReportsAsync(nextLicenseReport))
            {
                nextLicenseReport = await FetchNextReportUrlAsync();
            }

            return true;
        }

        private async Task<Uri> FetchNextReportUrlAsync()
        {
            Trace.TraceInformation("Fetching next report URL from {0}/{1}", _packageDatabase.DataSource, _packageDatabase.InitialCatalog);

            Uri nextLicenseReport = null;
            using (var connection = await _packageDatabase.ConnectTo())
            {
                var nextReportUrl = (await connection.QueryAsync<string>(
                    @"SELECT TOP 1 NextLicenseReport FROM GallerySettings")).SingleOrDefault();

                if (string.IsNullOrEmpty(nextReportUrl))
                {
                    Trace.TraceInformation("No next report URL found, using default");
                }
                else if (!Uri.TryCreate(nextReportUrl, UriKind.Absolute, out nextLicenseReport))
                {
                    Trace.TraceInformation("Next Report URL '{0}' is invalid. Using default", nextReportUrl);
                }

                nextLicenseReport = nextLicenseReport ?? _licenseReportService;
            }

            Trace.TraceInformation("Fetched next report URL '{2}' from {0}/{1}", _packageDatabase.DataSource, _packageDatabase.InitialCatalog, (nextLicenseReport == null ? string.Empty : nextLicenseReport.AbsoluteUri));

            return nextLicenseReport;
        }

        private async Task<bool> ProcessReportsAsync(Uri nextLicenseReport)
        {
            HttpWebResponse response = null;
            var tries = 0;

            Trace.TraceInformation("Downloading license report {0}", nextLicenseReport.AbsoluteUri);

            while (tries < _retryCount.Value && response == null)
            {
                var request = (HttpWebRequest)WebRequest.Create(nextLicenseReport);
                if (_licenseReportCredentials != null)
                {
                    request.Credentials = _licenseReportCredentials;
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
                        if (tries < _retryCount.Value)
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
                    Trace.TraceInformation("Error downloading report {0}, retrying. {1}", nextLicenseReport.AbsoluteUri, thrown);
                    await Task.Delay(10 * 1000);
                }
            }

            Trace.TraceInformation("Downloaded license report {0}", nextLicenseReport.AbsoluteUri);
            Trace.TraceInformation("Processing license report {0}", nextLicenseReport.AbsoluteUri);

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Trace.TraceInformation("Reading license report {0}", nextLicenseReport.AbsoluteUri);

                    string content;
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        content = await reader.ReadToEndAsync();
                    }

                    Trace.TraceInformation("Read license report {0}", nextLicenseReport.AbsoluteUri);

                    var sonatypeMessage = JObject.Parse(content);
                    if (!sonatypeMessage.IsValid(_sonatypeSchema))
                    {
                        Trace.TraceInformation("Invalid license report in {0}. {1}", nextLicenseReport.AbsoluteUri, Strings.UpdateLicenseReportsJob_JsonDoesNotMatchSchema);
                        return false;
                    }

                    var events = sonatypeMessage["events"].Cast<JObject>().ToList();
                    foreach (var messageEvent in events)
                    {
                        var report = CreateReport(messageEvent);

                        Trace.TraceInformation("Storing license report for {0} {1}", report.PackageId, report.Version);

                        if (await StoreReportAsync(report) == -1)
                        {
                            Trace.TraceInformation("Unable to store report for {0} {1}. Package does not exist in database.", report.PackageId, report.Version);
                        }
                        else
                        {
                            Trace.TraceInformation("Stored license report for {0} {1}", report.PackageId, report.Version);
                        }
                    }

                    // Store the next URL
                    if (sonatypeMessage["next"].Value<string>().Length > 0)
                    {
                        var nextReportUrl = sonatypeMessage["next"].Value<string>();
                        if (!Uri.TryCreate(nextReportUrl, UriKind.Absolute, out nextLicenseReport))
                        {
                            Trace.TraceInformation("Invalid next report URL: {0}", nextReportUrl);
                            return false;
                        }

                        Trace.TraceInformation("Storing next license report URL: {0}", nextLicenseReport.AbsoluteUri);

                        // Record the next report to the database so we can check it again if we get aborted before finishing.
                        using (var connection = await _packageDatabase.ConnectTo())
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

                    Trace.TraceInformation("Processing license report {0}", nextLicenseReport.AbsoluteUri);
                }
                else if (response.StatusCode != HttpStatusCode.NoContent)
                {
                    Trace.TraceInformation("No report for {0} yet.", nextLicenseReport.AbsoluteUri);
                }
                else
                {
                    Trace.TraceInformation("HTTP {1} error requesting {0}: {2}", nextLicenseReport.AbsoluteUri, (int)response.StatusCode, response.StatusDescription);
                }

                return false;
            }
        }

        private async Task<int> StoreReportAsync(PackageLicenseReport report)
        {
            using (var connection = await _packageDatabase.ConnectTo())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "AddPackageLicenseReport";
                command.CommandType = CommandType.StoredProcedure;

                var licensesNames = new DataTable();
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

                return (int)await command.ExecuteScalarAsync();
            }
        }

        private class PackageLicenseReport
        {
            public int Sequence { get; set; }

            public string PackageId { get; set; }

            public string Version { get; set; }

            public string ReportUrl { get; set; }

            public string Comment { get; set; }

            public ICollection<string> Licenses { get; private set; }

            public PackageLicenseReport(int sequence)
            {
                Sequence = sequence;
                PackageId = null;
                Version = null;
                ReportUrl = null;
                Comment = null;
                Licenses = new LinkedList<string>();
            }

            public override string ToString()
            {
                return string.Format("{{ {0}, {1}, [ {2} ] }}", Sequence, string.Join(", ", PackageId, Version, ReportUrl, Comment), string.Join(", ", Licenses));
            }
        }

    }
}
