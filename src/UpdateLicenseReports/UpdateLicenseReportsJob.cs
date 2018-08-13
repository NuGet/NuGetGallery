// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Autofac;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;

namespace UpdateLicenseReports
{
    internal class UpdateLicenseReportsJob : JsonConfigurationJob
    {
        private const string LicenseServiceSampleResponseFile = @"TestData\LicenseServiceSampleResponse.json";

        private const int DefaultRetryCount = 4;

        private static readonly JSchema _sonatypeSchema = JSchema.Parse(@"{ 'type': 'object',
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

        private UpdateLicenseReportsConfiguration _configuration;
        private Uri _licenseService;
        private NetworkCredential _licenseServiceCredentials;
        private int _retryCount;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            _configuration = _serviceProvider.GetRequiredService<IOptionsSnapshot<UpdateLicenseReportsConfiguration>>().Value;

            if (!_configuration.Test)
            {
                _licenseService = new Uri(_configuration.LicenseReportService);

                _licenseServiceCredentials = new NetworkCredential(
                    _configuration.LicenseReportUser ?? string.Empty,
                    _configuration.LicenseReportPassword ?? string.Empty);
            }

            _retryCount = _configuration.RetryCount ?? DefaultRetryCount;
        }

        public override async Task Run()
        {
            // Fetch next report url
            var nextLicenseReport = await FetchNextReportUrlAsync();

            // Process that report
            while (nextLicenseReport != null && await ProcessReportsAsync(nextLicenseReport))
            {
                nextLicenseReport = await FetchNextReportUrlAsync();
            }
        }

        private async Task<Uri> FetchNextReportUrlAsync()
        {
            Uri nextLicenseReport = null;
            using (var connection = await OpenSqlConnectionAsync<GalleryDbConfiguration>())
            {
                Logger.LogInformation("Fetching next report URL from {DataSource}/{InitialCatalog}",
                    connection.DataSource, connection.Database);

                var nextReportUrl = (await connection.QueryAsync<string>(
                    @"SELECT TOP 1 NextLicenseReport FROM GallerySettings")).SingleOrDefault();

                if (string.IsNullOrEmpty(nextReportUrl))
                {
                    Logger.LogInformation("No next report URL found, using default");
                }
                else if (!Uri.TryCreate(nextReportUrl, UriKind.Absolute, out nextLicenseReport))
                {
                    Logger.LogInformation("Next Report URL '{NextReportUrl}' is invalid. Using default", nextReportUrl);
                }

                nextLicenseReport = nextLicenseReport ?? _licenseService;

                Logger.LogInformation("Fetched next report URL '{NextReportUrl}' from {DataSource}/{InitialCatalog}",
                    (nextLicenseReport == null ? string.Empty : nextLicenseReport.AbsoluteUri),
                    connection.DataSource, connection.Database);
            }

            return nextLicenseReport;
        }
        private Task<string> DownloadNextReportAsync(Uri nextLicenseReport)
        {
            return _configuration.Test
                ? Task.FromResult(GetExampleReportAsync())
                : DownloadNextReportFromServiceAsync(nextLicenseReport);
        }

        private string GetExampleReportAsync()
        {
            return File.ReadAllText(LicenseServiceSampleResponseFile);
        }

        private async Task<string> DownloadNextReportFromServiceAsync(Uri nextLicenseReport)
        {
            HttpWebResponse response = null;
            var tries = 0;

            Logger.LogInformation("Downloading license report {ReportUrl}", nextLicenseReport.AbsoluteUri);

            while (tries < _retryCount && response == null)
            {
                var request = (HttpWebRequest)WebRequest.Create(nextLicenseReport);
                if (_licenseServiceCredentials != null)
                {
                    request.Credentials = _licenseServiceCredentials;
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
                        if (tries < _retryCount)
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
                    Logger.LogInformation("Error downloading report {ReportUrl}, retrying. {Exception}", nextLicenseReport.AbsoluteUri, thrown);
                    await Task.Delay(10 * 1000);
                }
            }

            Logger.LogInformation("Downloaded license report {ReportUrl}", nextLicenseReport.AbsoluteUri);
            Logger.LogInformation("Processing license report {ReportUrl}", nextLicenseReport.AbsoluteUri);

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Logger.LogInformation("Reading license report {ReportUrl}", nextLicenseReport.AbsoluteUri);

                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        return await reader.ReadToEndAsync();
                    }
                }
                else if (response.StatusCode != HttpStatusCode.NoContent)
                {
                    Logger.LogInformation("No report for {NextReportUrl} yet.", nextLicenseReport.AbsoluteUri);
                }
                else
                {
                    Logger.LogInformation("HTTP {StatusCode} error requesting {NextReportUrl}: {StatusDescription}", response.StatusCode, nextLicenseReport.AbsoluteUri, response.StatusDescription);
                }
            }

            return null;
        }

        private async Task<bool> ProcessReportsAsync(Uri nextLicenseReport)
        {
            var content = await DownloadNextReportAsync(nextLicenseReport);

            if (content == null)
            {
                return false;
            }

            Logger.LogInformation("Read license report {ReportUrl}", nextLicenseReport.AbsoluteUri);

            var sonatypeMessage = JObject.Parse(content);
            if (!sonatypeMessage.IsValid(_sonatypeSchema))
            {
                Logger.LogInformation("Invalid license report in {ReportUrl}. {Error}", nextLicenseReport.AbsoluteUri, Strings.UpdateLicenseReportsJob_JsonDoesNotMatchSchema);
                return false;
            }

            var events = sonatypeMessage["events"].Cast<JObject>().ToList();
            foreach (var messageEvent in events)
            {
                var report = CreateReport(messageEvent);

                if (_configuration.Test)
                {
                    Logger.LogInformation("Test complete for {PackageId} {PackageVersion}.", report.PackageId, report.Version);
                    return false;
                }

                Logger.LogInformation("Storing license report for {PackageId} {PackageVersion}", report.PackageId, report.Version);

                if (await StoreReportAsync(report) == -1)
                {
                    Logger.LogInformation("Unable to store report for {PackageId} {PackageVersion}. Package does not exist in database.", report.PackageId, report.Version);
                }
                else
                {
                    Logger.LogInformation("Stored license report for {PackageId} {PackageVersion}", report.PackageId, report.Version);
                }
            }

            // Store the next URL
            if (sonatypeMessage["next"].Value<string>().Length > 0)
            {
                var nextReportUrl = sonatypeMessage["next"].Value<string>();
                if (!Uri.TryCreate(nextReportUrl, UriKind.Absolute, out nextLicenseReport))
                {
                    Logger.LogInformation("Invalid next report URL: {NextReportUrl}", nextReportUrl);
                    return false;
                }

                Logger.LogInformation("Storing next license report URL: {NextReportUrl}", nextLicenseReport.AbsoluteUri);

                // Record the next report to the database so we can check it again if we get aborted before finishing.
                using (var connection = await OpenSqlConnectionAsync<GalleryDbConfiguration>())
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

            Logger.LogInformation("Processing license report {NextReportUrl}", nextLicenseReport.AbsoluteUri);

            return true;
        }

        private async Task<int> StoreReportAsync(PackageLicenseReport report)
        {
            using (var connection = await OpenSqlConnectionAsync<GalleryDbConfiguration>())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "AddPackageLicenseReport2";
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

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            ConfigureInitializationSection<UpdateLicenseReportsConfiguration>(services, configurationRoot);
        }
    }
}
