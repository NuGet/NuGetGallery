using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using NuGet.Services.Configuration;
using NuGet.Services.Work.Configuration;

namespace NuGet.Services.Work.Jobs
{
    [Description("Updates friendly license data from the license report service")]
    public class UpdateLicenseReportsJob : JobHandler<UpdateLicenseReportsEventSource>
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
        protected ConfigurationHub Config { get; set; }

        public UpdateLicenseReportsJob(ConfigurationHub config)
        {
            Config = config;
        }

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

        protected internal override async Task Execute()
        {
            // Load defaults
            LoadDefaults();

            // Fetch next report url
            Uri nextLicenseReport = await FetchNextReportUrl();

            // Process that report
            while (nextLicenseReport != null && await ProcessReports(nextLicenseReport))
            {
                nextLicenseReport = await FetchNextReportUrl();
            }
        }

        private async Task<Uri> FetchNextReportUrl()
        {
            Log.FetchingNextReportUrl(PackageDatabase.DataSource, PackageDatabase.InitialCatalog);
            Uri nextLicenseReport = null;
            using (var connection = await PackageDatabase.ConnectTo())
            {
                var nextReportUrl = (await connection.QueryAsync<string>(
                    @"SELECT TOP 1 NextLicenseReport FROM GallerySettings")).SingleOrDefault();
                if (String.IsNullOrEmpty(nextReportUrl))
                {
                    Log.NoNextReportFound();
                }
                else if (!Uri.TryCreate(nextReportUrl, UriKind.Absolute, out nextLicenseReport))
                {
                    Log.InvalidNextReportFound(nextReportUrl);
                }
                nextLicenseReport = nextLicenseReport ?? LicenseReportService;
            }
            Log.FetchedNextReportUrl(PackageDatabase.DataSource, PackageDatabase.InitialCatalog, (nextLicenseReport == null ? String.Empty : nextLicenseReport.AbsoluteUri));
            return nextLicenseReport;
        }

        private async Task<bool> ProcessReports(Uri nextLicenseReport)
        {
            HttpWebResponse response = null;
            int tries = 0;
            Log.RequestingLicenseReport(nextLicenseReport.AbsoluteUri);
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
                    Log.ErrorDownloadingLicenseReport(nextLicenseReport.AbsoluteUri, thrown.ToString());
                    await Task.Delay(10 * 1000);
                }
            }
            Log.RequestedLicenseReport(nextLicenseReport.AbsoluteUri);

            Log.ProcessingLicenseReport(nextLicenseReport.AbsoluteUri);
            using (response)
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Log.ReadingLicenseReport(nextLicenseReport.AbsoluteUri);
                    string content;
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        content = await reader.ReadToEndAsync();
                    }
                    Log.ReadLicenseReport(nextLicenseReport.AbsoluteUri);

                    JObject sonatypeMessage = JObject.Parse(content);
                    if (!sonatypeMessage.IsValid(sonatypeSchema))
                    {
                        Log.InvalidLicenseReport(nextLicenseReport.AbsoluteUri, Strings.UpdateLicenseReportsJob_JsonDoesNotMatchSchema);
                        return false;
                    }

                    var events = sonatypeMessage["events"].Cast<JObject>().ToList();
                    for (int i = 0; i < events.Count; i++)
                    {
                        var messageEvent = events[i];
                        PackageLicenseReport report = CreateReport(messageEvent);
                        Log.StoringLicenseReport(report.PackageId, report.Version);

                        if (await StoreReport(report) == -1)
                        {
                            Log.PackageNotFound(report.PackageId, report.Version);
                        }
                        else
                        {
                            Log.StoredLicenseReport(report.PackageId, report.Version);
                        }
                    }

                    // Store the next URL
                    if (sonatypeMessage["next"].Value<string>().Length > 0)
                    {
                        var nextReportUrl = sonatypeMessage["next"].Value<string>();
                        if (!Uri.TryCreate(nextReportUrl, UriKind.Absolute, out nextLicenseReport))
                        {
                            Log.InvalidNextReportUrl(nextReportUrl);
                            return false;
                        }
                        Log.StoringNextReportUrl(nextLicenseReport.AbsoluteUri);

                        // Record the next report to the database so we can check it again if we get aborted before finishing.
                        if (!WhatIf)
                        {
                            using (var connection = await PackageDatabase.ConnectTo())
                            {
                                await connection.QueryAsync<int>(@"
                                        UPDATE GallerySettings
                                        SET NextLicenseReport = @nextLicenseReport",
                                    new { nextLicenseReport = nextLicenseReport.AbsoluteUri });
                            }
                            return true; // Continue and read the next report later
                        }
                    }
                    else
                    {
                        nextLicenseReport = null;
                    }
                    Log.ProcessedLicenseReport(nextLicenseReport.AbsoluteUri);
                }
                else if (response.StatusCode != HttpStatusCode.NoContent)
                {
                    Log.NoReportYet(nextLicenseReport.AbsoluteUri);
                }
                else
                {
                    Log.ReportHttpError(nextLicenseReport.AbsoluteUri, (int)response.StatusCode, response.StatusDescription);
                }
                return false;
            }
        }

        private void LoadDefaults()
        {
            RetryCount = RetryCount ?? DefaultRetryCount;
            PackageDatabase = PackageDatabase ?? Config.Sql.Legacy;

            var licenseConfig = Config.GetSection<LicenseReportConfiguration>();
            if (licenseConfig != null)
            {
                LicenseReportService = LicenseReportService ?? licenseConfig.Service;
                LicenseReportUser = LicenseReportUser ?? licenseConfig.User;
                LicenseReportPassword = LicenseReportPassword ?? licenseConfig.Password;
            }

            // Build credentials
            if (!String.IsNullOrEmpty(LicenseReportUser))
            {
                if (!String.IsNullOrEmpty(LicenseReportPassword))
                {
                    LicenseReportCredentials = new NetworkCredential(LicenseReportUser, LicenseReportPassword);
                }
                else
                {
                    LicenseReportCredentials = new NetworkCredential(LicenseReportUser, String.Empty);
                }
            }
            else if (!String.IsNullOrEmpty(LicenseReportPassword))
            {
                LicenseReportCredentials = new NetworkCredential(String.Empty, LicenseReportPassword);
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
                command.Parameters.AddWithValue("@reportUrl", report.ReportUrl ?? String.Empty);
                command.Parameters.AddWithValue("@comment", report.Comment);
                command.Parameters.AddWithValue("@whatIf", WhatIf);

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
                    + String.Join(", ", new string[] { PackageId, Version, ReportUrl, Comment })
                    + ", [ " + String.Join(", ", Licenses) + " ] }";
            }
        }
    }

    [EventSource(Name="Outercurve-NuGet-Jobs-UpdateLicenseReports")]
    public class UpdateLicenseReportsEventSource : EventSource
    {
        public static readonly UpdateLicenseReportsEventSource Log = new UpdateLicenseReportsEventSource();
        private UpdateLicenseReportsEventSource() { }

        [Event(
            eventId: 1,
            Task = Tasks.FetchingNextReportUrl,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Fetching next report URL from {0}/{1}")]
        public void FetchingNextReportUrl(string server, string database) { WriteEvent(1, server, database); }

        [Event(
            eventId: 2,
            Task = Tasks.FetchingNextReportUrl,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Fetched next report URL '{2}' from {0}/{1}")]
        public void FetchedNextReportUrl(string server, string database, string url) { WriteEvent(2, server, database, url); }

        [Event(
            eventId: 3,
            Task = Tasks.FetchingNextReportUrl,
            Level = EventLevel.Informational,
            Message = "No next report URL found, using default")]
        public void NoNextReportFound() { WriteEvent(3); }

        [Event(
            eventId: 4,
            Task = Tasks.FetchingNextReportUrl,
            Level = EventLevel.Error,
            Message = "Next Report URL '{0}' is invalid. Using default")]
        public void InvalidNextReportFound(string url) { WriteEvent(4, url); }

        [Event(
            eventId: 5,
            Task = Tasks.RequestingLicenseReport,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Downloading license report {0}")]
        public void RequestingLicenseReport(string url) { WriteEvent(5, url); }

        [Event(
            eventId: 6,
            Task = Tasks.RequestingLicenseReport,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Downloaded license report {0}")]
        public void RequestedLicenseReport(string url) { WriteEvent(6, url); }

        [Event(
            eventId: 7,
            Task = Tasks.RequestingLicenseReport,
            Level = EventLevel.Warning,
            Message = "Error downloading report {0}, retrying. {1}")]
        public void ErrorDownloadingLicenseReport(string url, string exception) { WriteEvent(7, url, exception); }

        [Event(
            eventId: 8,
            Task = Tasks.ProcessingLicenseReport,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Processing license report {0}")]
        public void ProcessingLicenseReport(string url) { WriteEvent(8, url); }

        [Event(
            eventId: 9,
            Task = Tasks.ProcessingLicenseReport,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Processed license report {0}")]
        public void ProcessedLicenseReport(string url) { WriteEvent(9, url); }

        [Event(
            eventId: 10,
            Task = Tasks.ProcessingLicenseReport,
            Level = EventLevel.Error,
            Message = "Invalid license report in {0}. {1}")]
        public void InvalidLicenseReport(string url, string exception) { WriteEvent(10, url, exception); }

        [Event(
            eventId: 11,
            Task = Tasks.ReadingLicenseReport,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Reading license report {0}")]
        public void ReadingLicenseReport(string url) { WriteEvent(11, url); }

        [Event(
            eventId: 12,
            Task = Tasks.ReadingLicenseReport,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Read license report {0}")]
        public void ReadLicenseReport(string url) { WriteEvent(12, url); }

        [Event(
            eventId: 13,
            Task = Tasks.StoringLicenseReport,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Storing license report for {0} {1}")]
        public void StoringLicenseReport(string id, string version) { WriteEvent(13, id, version); }

        [Event(
            eventId: 14,
            Task = Tasks.StoringLicenseReport,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Stored license report for {0} {1}")]
        public void StoredLicenseReport(string id, string version) { WriteEvent(14, id, version); }

        [Event(
            eventId: 15,
            Task = Tasks.StoringLicenseReport,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Error,
            Message = "Unable to store report for {0} {1}. Package does not exist in database.")]
        public void PackageNotFound(string id, string version) { WriteEvent(15, id, version); }

        [Event(
            eventId: 16,
            Task = Tasks.StoringNextReportUrl,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Storing next license report URL: {0}")]
        public void StoringNextReportUrl(string url) { WriteEvent(16, url); }

        [Event(
            eventId: 17,
            Task = Tasks.StoringNextReportUrl,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Stored license report URL: {0}")]
        public void StoredNextReportUrl(string url) { WriteEvent(17, url); }

        [Event(
            eventId: 18,
            Task = Tasks.StoringNextReportUrl,
            Level = EventLevel.Error,
            Message = "Invalid next report URL: {0}")]
        public void InvalidNextReportUrl(string url) { WriteEvent(18, url); }

        [Event(
            eventId: 19,
            Task = Tasks.ProcessingLicenseReport,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "No report for {0} yet.")]
        public void NoReportYet(string url) { WriteEvent(19, url); }

        [Event(
            eventId: 20,
            Task = Tasks.ProcessingLicenseReport,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "HTTP {1} error requesting {0}: {2}")]
        public void ReportHttpError(string url, int statusCode, string message) { WriteEvent(20, url, statusCode, message); }

        public static class Tasks
        {
            public const EventTask FetchingNextReportUrl = (EventTask)0x1;
            public const EventTask RequestingLicenseReport = (EventTask)0x2;
            public const EventTask ProcessingLicenseReport = (EventTask)0x3;
            public const EventTask ReadingLicenseReport = (EventTask)0x4;
            public const EventTask StoringLicenseReport = (EventTask)0x5;
            public const EventTask StoringNextReportUrl = (EventTask)0x6;
        }
    }
}
