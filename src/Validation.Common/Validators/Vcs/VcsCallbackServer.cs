// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Owin;
using Microsoft.WindowsAzure.Storage;
using NuGet.ApplicationInsights.Owin;
using NuGet.Jobs.Validation.Common.Validators.Vcs;
using NuGet.Services.VirusScanning.Vcs.Callback;
using Owin;

[assembly: OwinStartup(typeof(VcsCallbackServerStartup))]

namespace NuGet.Jobs.Validation.Common.Validators.Vcs
{
    public class VcsCallbackServerStartup
    {
        private readonly VcsStatusCallbackParser _callbackParser = new VcsStatusCallbackParser();

        private readonly PackageValidationTable _packageValidationTable;
        private readonly PackageValidationAuditor _packageValidationAuditor;
        private readonly INotificationService _notificationService;
        private readonly ILogger<VcsCallbackServerStartup> _logger;

        /// <summary>
        /// Number of body characters to take for logging.
        /// </summary>
        /// <remarks>
        /// Callback service is available to be queried from anywhere and hence the body may be of any size.
        /// In situations when we want to log the body, we don't want to log potentially Multi-MB bodies, so, 
        /// we'll only take a "reasonable" (that would fit most of the calls we really expect) amount from 
        /// the beginning.
        /// </remarks>
        private const int ReasonableBodySize = 2048;

        private static class State
        {
            public const string Complete = "Complete";
            public const string Released = "Released";
        }

        public VcsCallbackServerStartup()
        {
            // Configure to get values from keyvault
            var configurationService = new ConfigurationService(new SecretReaderFactory());

            // Get configuration
            var cloudStorageAccount = CloudStorageAccount.Parse(configurationService.Get("DataStorageAccount").Result);
            var containerName = configurationService.Get("ContainerName").Result;

            string instrumentationKey = configurationService.Get("ApplicationInsightsInstrumentationKey").Result;
            Services.Logging.ApplicationInsights.Initialize(instrumentationKey);
            ILoggerFactory loggerFactory = Services.Logging.LoggingSetup.CreateLoggerFactory();
            _logger = loggerFactory.CreateLogger<VcsCallbackServerStartup>();

            // Services
            _packageValidationTable = new PackageValidationTable(cloudStorageAccount, containerName);
            _packageValidationAuditor = new PackageValidationAuditor(cloudStorageAccount, containerName, loggerFactory);
            _notificationService = new NotificationService(cloudStorageAccount, containerName);
        }

        public void Configuration(IAppBuilder app)
        {
            if (Services.Logging.ApplicationInsights.Initialized)
            {
                app.Use<RequestTrackingMiddleware>();
            }

            app.Run(Invoke);
        }

        public async Task Invoke(IOwinContext context)
        {
            if (context.Request.Method == "POST" && context.Request.ContentType.Contains("text/xml"))
            {
                // VCS callback request
                await ProcessRequest(context.Request.Body);

                // The VCS caller requires a SOAP response.
                context.Response.ContentType = "text/xml";
                await context.Response.WriteAsync(@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <StatusChangedResponse xmlns=""http://roq/"" />
  </soap:Body>
</soap:Envelope>");
            }
            else if (context.Request.Method == "GET")
            {
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync("OK");
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
        }

        private async Task ProcessRequest(Stream requestBody)
        {
            using (var bodyStreamReader = new StreamReader(requestBody))
            {
                bool processedRequest = false;

                var body = await bodyStreamReader.ReadToEndAsync();
                var result = _callbackParser.ParseSoapMessage(body);

                // Find our validation
                Guid validationId;
                PackageValidationEntity validationEntity = null;
                if (Guid.TryParse(result.SrcId, out validationId))
                {
                    validationEntity = await _packageValidationTable.GetValidationAsync(validationId);
                    if (validationEntity == null)
                    {
                        processedRequest = true;
                        _logger.TrackValidatorResult(VcsValidator.ValidatorName, validationId, TraceConstant.RequestNotFound, validationEntity.PackageId, validationEntity.PackageVersion);

                        // Notify us about the fact that no valiation was found
                        await _notificationService.SendNotificationAsync(
                            "vcscallback-notfound",
                            "Validation " + validationId + " was not found.",
                            body);
                    }
                    else
                    {
                        if (validationEntity.GetCompletedValidatorsList().Contains(VcsValidator.ValidatorName))
                        {
                            _logger.LogInformation($"Package already processed for validation {{{TraceConstant.ValidationId}}} " +
                                    "with state={State}, result={Result} " +
                                    $"for package {{{TraceConstant.PackageId}}} " +
                                    $"v. {{{TraceConstant.PackageVersion}}}",
                                validationId,
                                result.State,
                                result.Result,
                                validationEntity.PackageId,
                                validationEntity.PackageVersion);
                            return;
                        }
                    }
                }

                // Determine state of the VCS callback
                if (validationEntity != null)
                {
                    _logger.LogInformation($"Got VCS callback for validation {{{TraceConstant.ValidationId}}} " +
                            "with state={State}, result={Result} " +
                            $"for package {{{TraceConstant.PackageId}}} " +
                            $"v. {{{TraceConstant.PackageVersion}}}", 
                        validationId,
                        result.State,
                        result.Result,
                        validationEntity.PackageId,
                        validationEntity.PackageVersion);

                    // "The Request is in Manual State and the Request is cancelled."
                    // This denotes a manual verification is being carried out or has been carried out.
                    if ((result.State == State.Complete || result.State == State.Released)
                        && (result.Result == "Canceled" || result.Result == "Cancelled"))
                    {
                        processedRequest = true;
                        var services = result.Services?.Service;
                        if (services != null && services.Any(s => s.Name == "Scan" && s.State == "Complete" && s.Result == "Canceled"))
                        {
                            // Package scanned unclean
                            validationEntity.ValidatorCompleted(VcsValidator.ValidatorName, ValidationResult.Failed);
                            await _packageValidationTable.StoreAsync(validationEntity);

                            _logger.TrackValidatorResult(VcsValidator.ValidatorName, validationId, TraceConstant.PackageUnclean, validationEntity.PackageId, validationEntity.PackageVersion);
                            var auditEntries = new List<PackageValidationAuditEntry>();
                            auditEntries.Add(new PackageValidationAuditEntry
                            {
                                Timestamp = DateTimeOffset.UtcNow,
                                ValidatorName = VcsValidator.ValidatorName,
                                Message = "Package did not scan clean."
                            });

                            if (result.ResultReasons?.ResultReason != null)
                            {
                                foreach (var resultReason in result.ResultReasons.ResultReason)
                                {
                                    auditEntries.Add(new PackageValidationAuditEntry
                                    {
                                        Timestamp = DateTimeOffset.UtcNow,
                                        ValidatorName = VcsValidator.ValidatorName,
                                        Message = resultReason.RefId + " " + resultReason.Result + " " + resultReason.Determination
                                    });
                                }
                            }

                            await _packageValidationAuditor.WriteAuditEntriesAsync(
                                validationEntity.ValidationId, validationEntity.PackageId, validationEntity.PackageVersion, auditEntries);

                            // Notify
                            await _notificationService.SendNotificationAsync(
                                $"vcscallback-notclean/{validationEntity.Created.ToString("yyyy-MM-dd")}",
                                $"Validation {validationId} ({validationEntity.PackageId} {validationEntity.PackageVersion}) returned {result.State} {result.Result}.",
                                body);
                        }
                        else
                        {
                            _logger.TrackValidatorResult(VcsValidator.ValidatorName, validationId, TraceConstant.InvestigationNeeded, validationEntity.PackageId, validationEntity.PackageVersion);
                            // To investigate
                            await _notificationService.SendNotificationAsync(
                                $"vcscallback-investigate/{validationEntity.Created.ToString("yyyy-MM-dd")}",
                                $"Validation {validationId} ({validationEntity.PackageId} {validationEntity.PackageVersion}) returned {result.State} {result.Result}.",
                                body);
                        }
                    }

                    // "The Request is completed, with either of these four states: Results, Pass, PassWithInfo, PassManual"
                    // This denotes scan has completed and we have a pass (or results)
                    if (result.State == State.Complete || result.State == State.Released)
                    {
                        if (result.Result == "Pass" || result.Result == "PassWithInfo" || result.Result == "PassManual")
                        {
                            // The result is clean.
                            processedRequest = true;
                            validationEntity.ValidatorCompleted(VcsValidator.ValidatorName, ValidationResult.Succeeded);
                            await _packageValidationTable.StoreAsync(validationEntity);

                            _logger.TrackValidatorResult(VcsValidator.ValidatorName, validationId, ValidationResult.Succeeded.ToString(), validationEntity.PackageId, validationEntity.PackageVersion);
                            await _packageValidationAuditor.WriteAuditEntryAsync(validationEntity.ValidationId, validationEntity.PackageId, validationEntity.PackageVersion,
                                new PackageValidationAuditEntry
                                {
                                    Timestamp = DateTimeOffset.UtcNow,
                                    ValidatorName = VcsValidator.ValidatorName,
                                    Message = "Package scanned clean."
                                });
                        }
                        else if (result.Result == "Results" || result.Result == "Fail")
                        {
                            // Potential issue, report back
                            processedRequest = true;
                            validationEntity.ValidatorCompleted(VcsValidator.ValidatorName, ValidationResult.Failed);
                            await _packageValidationTable.StoreAsync(validationEntity);

                            _logger.TrackValidatorResult(VcsValidator.ValidatorName, 
                                validationId, 
                                ValidationResult.Failed.ToString(), 
                                validationEntity.PackageId, 
                                validationEntity.PackageVersion, 
                                TruncateString(body, ReasonableBodySize));
                            var auditEntries = new List<PackageValidationAuditEntry>();
                            auditEntries.Add(new PackageValidationAuditEntry
                            {
                                Timestamp = DateTimeOffset.UtcNow,
                                ValidatorName = VcsValidator.ValidatorName,
                                Message = $"Package scan failed. Response: {body}"
                            });

                            if (result.ResultReasons?.ResultReason != null)
                            {
                                foreach (var resultReason in result.ResultReasons.ResultReason)
                                {
                                    auditEntries.Add(new PackageValidationAuditEntry
                                    {
                                        Timestamp = DateTimeOffset.UtcNow,
                                        ValidatorName = VcsValidator.ValidatorName,
                                        Message = resultReason.RefId + " " + resultReason.Result + " " + resultReason.Determination
                                    });
                                }
                            }

                            await _packageValidationAuditor.WriteAuditEntriesAsync(
                                validationEntity.ValidationId, validationEntity.PackageId, validationEntity.PackageVersion, auditEntries);

                            // Notify
                            await _notificationService.SendNotificationAsync(
                                $"vcscallback-failed/{validationEntity.Created.ToString("yyyy-MM-dd")}",
                                $"Validation {validationId} ({validationEntity.PackageId} {validationEntity.PackageVersion}) did not scan clean.",
                                body);
                        }
                    }
                }

                if (!processedRequest)
                {
                    _logger.LogWarning(
                        "Callback was not handled for State={State}, Result={Result}. " +
                        "Request body: {RequestBody}",
                        result?.State, result?.Result, TruncateString(body, ReasonableBodySize));
                }
            }
        }

        /// <summary>
        /// Truncates the string leaving at most specified amount of characters and adds a "(truncated)" at the end
        /// if it removes any portion of the string
        /// </summary>
        /// <param name="str">String to truncate</param>
        /// <param name="length">Max amount of characters to keep if truncated</param>
        /// <returns>Original string if it's length was less than specified length, otherwise, first 'length' characters of the string 
        /// with "(truncated)" appended.</returns>
        private static string TruncateString(string str, int length)
        {
            if (str.Length <= length)
            {
                return str;
            }

            return str.Substring(0, length) + "(truncated)";
        }
    }
}