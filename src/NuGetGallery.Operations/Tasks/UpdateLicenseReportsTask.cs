// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AnglicanGeek.DbExecutor;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations.Tasks
{
    [Command("updatelicensereports", "Updates the license reports from SonaType", AltName = "ulr")]
    public class UpdateLicenseReportsTask : DatabaseTask
    {
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

        [Option("The base URL for the reporting service", AltName = "s")]
        public Uri LicenseReportService { get; set; }

        [Option("The username for the reporting service", AltName = "u")]
        public string LicenseReportUser { get; set; }

        [Option("The password for the reporting service", AltName = "p")]
        public string LicenseReportPassword { get; set; }

        public NetworkCredential LicenseReportCredentials { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            if (CurrentEnvironment != null)
            {
                LicenseReportCredentials = LicenseReportCredentials ?? CurrentEnvironment.LicenseReportServiceCredentials;
                LicenseReportService = LicenseReportService ?? CurrentEnvironment.LicenseReportService;
            }

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

            ArgCheck.RequiredOrConfig(LicenseReportCredentials, "LicenseReportUser");
            ArgCheck.RequiredOrConfig(LicenseReportService, "LicenseReportService");
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

        public override void ExecuteCommand()
        {
            Log.Info("Loading URL for the next license report");
            Uri nextLicenseReport = null;
            try
            {
                if (!WithConnection((connection, executor) =>
                {
                    var nextReportUrl = executor.Query<string>(
                        @"SELECT NextLicenseReport FROM GallerySettings").FirstOrDefault();
                    if (String.IsNullOrEmpty(nextReportUrl))
                    {
                        Log.Info("No NextLicenseReport value in GallerySettings. Using default");
                    }
                    else if (!Uri.TryCreate(nextReportUrl, UriKind.Absolute, out nextLicenseReport))
                    {
                        Log.Error("Invalid NextLicenseReport value in GallerySettings: {0}", nextReportUrl);
                        return false;
                    }
                    return true;
                }))
                {
                    return;
                }
            }
            catch (Exception e)
            {
                Log.Error("Database error\n\nCallstack:\n" + e.ToString());
                return;
            }
            nextLicenseReport = nextLicenseReport ?? LicenseReportService;

            while (nextLicenseReport != null)
            {
                HttpWebResponse response = null;
                int tries = 0;
                while (tries < 10 && response == null)
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(nextLicenseReport);
                    if (LicenseReportCredentials != null)
                    {
                        request.Credentials = LicenseReportCredentials;
                    }
                    Log.Http(request);

                    try
                    {
                        response = (HttpWebResponse)request.GetResponse();
                    }
                    catch (WebException ex)
                    {
                        response = null;
                        var httpResp = ex.Response as HttpWebResponse;
                        if (httpResp != null)
                        {
                            Log.Http(httpResp);
                            return;
                        }
                        else if (ex.Status == WebExceptionStatus.Timeout || ex.Status == WebExceptionStatus.ConnectFailure)
                        {
                            // Try again in 10 seconds
                            tries++;
                            if (tries < 10)
                            {
                                Log.Warn("Timeout connecting to service. Sleeping for 30 seconds and trying again ({0}/10 tries)", tries);
                                Thread.Sleep(10 * 1000);
                            }
                            else
                            {
                                Log.Error("Timeout connecting to service. Tried 10 times. Aborting Job");
                                throw;
                            }
                        }
                        else
                        {
                            Log.ErrorException(String.Format("WebException contacting service: {0}", ex.Status), ex);
                            throw;
                        }
                    }
                }

                using (response)
                {
                    Log.Http(response);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string content = (new StreamReader(response.GetResponseStream())).ReadToEnd();
                        JObject sonatypeMessage = JObject.Parse(content);
                        if (!sonatypeMessage.IsValid(sonatypeSchema))
                        {
                            Log.Error("License report is invalid");
                            return;
                        }

                        var events = sonatypeMessage["events"].Cast<JObject>().ToList();
                        for (int i = 0; i < events.Count; i++)
                        {
                            var messageEvent = events[i];
                            PackageLicenseReport report = CreateReport(messageEvent);

                            bool success = true;
                            try
                            {
                                WithConnection((connection) =>
                                {
                                    if (StoreReport(report, connection) == -1)
                                    {
                                        Log.Error("[{0:000}/{1:000}] Package Not Found {2} {3}", i + 1, events.Count, report.PackageId, report.Version);
                                        success = false;
                                    }
                                });
                            }
                            catch (Exception e)
                            {
                                Log.Error("Database error\n\nCallstack:\n{0}", e.ToString());
                                return;
                            }
                            if (success)
                            {
                                Log.Info("[{0:000}/{1:000}] Updated {2} {3}", i + 1, events.Count, report.PackageId, report.Version);
                            }
                        }

                        if (sonatypeMessage["next"].Value<string>().Length > 0)
                        {
                            var nextReportUrl = sonatypeMessage["next"].Value<string>();
                            if (!Uri.TryCreate(nextReportUrl, UriKind.Absolute, out nextLicenseReport))
                            {
                                Log.Error("Invalid NextLicenseReport value from license report service: {0}", nextReportUrl);
                                return;
                            }
                            Log.Info("Found URL for the next license report: {0}", nextLicenseReport);

                            // Record the next report to the database so we can check it again if we get aborted before finishing.
                            if (!WhatIf)
                            {
                                try
                                {
                                    WithConnection((connection, executor) =>
                                    {
                                        executor.Execute(@"
                                    UPDATE GallerySettings
                                    SET NextLicenseReport = @nextLicenseReport",
                                            new { nextLicenseReport = nextLicenseReport.AbsoluteUri });
                                    });
                                }
                                catch (Exception e)
                                {
                                    Log.Error("Database error\n\nCallstack:\n{0}", e.ToString());
                                    return;
                                }
                            }
                        }
                        else
                        {
                            nextLicenseReport = null;
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

        private int StoreReport(PackageLicenseReport report, SqlConnection connection)
        {
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

                return (int)command.ExecuteScalar();
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
}