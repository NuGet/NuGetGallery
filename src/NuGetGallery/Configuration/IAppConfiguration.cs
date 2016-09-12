﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;

namespace NuGetGallery.Configuration
{
    public interface IAppConfiguration
    {
        /// <summary>
        /// Gets the location in which the Lucene Index is stored
        /// </summary>
        LuceneIndexLocation LuceneIndexLocation { get; set; }

        /// <summary>
        /// Gets the name of the environment in which the gallery is deployed
        /// </summary>
        string Environment { get; set; }

        /// <summary>
        /// Gets the warning banner text 
        /// </summary>
        string WarningBanner { get; set; }

        /// <summary>
        /// Gets a setting indicating if SSL is required for all operations once logged in.
        /// </summary>
        bool RequireSSL { get; set; }

        /// <summary>
        /// Gets the port used for SSL
        /// </summary>
        int SSLPort { get; set; }

        /// <summary>
        /// Gets the connection string to use when connecting to azure storage
        /// </summary>
        string AzureStorageConnectionString { get; set; }

        /// <summary>
        /// Gets a setting if Read Access Geo Redundant is enabled in azure storage
        /// </summary>
        bool AzureStorageReadAccessGeoRedundant { get; set; }

        /// <summary>
        /// Gets the URI to the search service
        /// </summary>
        Uri ServiceDiscoveryUri { get; set; }

        /// <summary>
        /// Gets the @type for the Search endpoint
        /// </summary>
        string SearchServiceResourceType { get; set; }

        /// <summary>
        /// Gets the @type for the Autocomplete endpoint
        /// </summary>
        string AutocompleteServiceResourceType { get; set; }

        /// <summary>
        /// Gets a boolean indicating if the site requires that email addresses be confirmed
        /// </summary>
        bool ConfirmEmailAddresses { get; set; }

        /// <summary>
        /// Gets a boolean indicating if the site is in read only mode
        /// </summary>
        bool ReadOnlyMode { get; set; }

        /// <summary>
        /// Gets if only service feeds should be registered
        /// </summary>
        bool FeedOnlyMode { get; set; }

        /// <summary>
        /// Gets the local directory in which to store files.
        /// </summary>
        string FileStorageDirectory { get; set; }

        /// <summary>
        /// Gets the site brand name i.e. 'NuGet Gallery' by default. Cobranding feature.
        /// </summary>
        string Brand { get; set; }

        /// <summary>
        /// Gets the gallery owner name and email address
        /// </summary>
        MailAddress GalleryOwner { get; set; }

        /// <summary>
        /// Gets the gallery e-mail from name and email address
        /// </summary>
        MailAddress GalleryNoReplyAddress { get; set; }

        /// <summary>
        /// Gets the storage mechanism used by this instance of the gallery
        /// </summary>
        StorageType StorageType { get; set; }

        /// <summary>
        /// Gets the URI of the SMTP host to use. Or null if SMTP is not being used. Use <see cref="NuGetGallery.Configuration.SmtpUri"/> to parse it
        /// </summary>
        Uri SmtpUri { get; set; }

        /// <summary>
        /// Gets the SQL Connection string used to connect to the database
        /// </summary>
        string SqlConnectionString { get; set; }

        /// <summary>
        /// Gets the SQL Connection string used to connect to the database for support requests
        /// </summary>
        string SqlConnectionStringSupportRequest { get; set; }

        /// <summary>
        /// Gets the host name of the Azure CDN being used
        /// </summary>
        string AzureCdnHost { get; set; }

        /// <summary>
        /// Gets the App ID of the Facebook app associated with this deployment
        /// </summary>
        string FacebookAppId { get; set; }

        /// <summary>
        /// Gets the Application Insights instrumentation key associated with this deployment.
        /// </summary>
        string AppInsightsInstrumentationKey { get; set; }

        /// <summary>
        /// Gets the Application Insights sampling percentage associated with this deployment.
        /// </summary>
        double AppInsightsSamplingPercentage { get; set; }

        /// <summary>
        /// Gets the protocol-independent site root
        /// </summary>
        string SiteRoot { get; set; }

        /// <summary>
        /// Gets the Google Analytics Property ID being used, if any.
        /// </summary>
        string GoogleAnalyticsPropertyId { get; set; }

        /// <summary>
        /// Gets a boolean indicating if perf logs should be collected
        /// </summary>
        bool CollectPerfLogs { get; set; }

        /// <summary>
        /// Gets a boolean indicating if the search index should be updated automatically in the background
        /// </summary>
        bool AutoUpdateSearchIndex { get; set; }

        /// <summary>
        /// Gets a string indicating which authentication provider(s) are supported for administrators. 
        /// When specified, the gallery will ensure admin users are logging in using any of the specified authentication providers.
        /// Blank means any authentication provider can be used by administrators.
        /// </summary>
        string EnforcedAuthProviderForAdmin { get; set; }

        /// <summary>
        /// The required format for a user password.
        /// </summary>
        string UserPasswordRegex { get; set; }

        /// <summary>
        /// A message to show the user, to explain password requirements.
        /// </summary>
        string UserPasswordHint { get; set; }

        /// <summary>
        /// Defines the time after which V1 API keys expire.
        /// </summary>
        int ExpirationInDaysForApiKeyV1 { get; set; }

        /// <summary>
        /// Defines the number of days before the API key expires when the server should emit a warning to the client.
        /// </summary>
        int WarnAboutExpirationInDaysForApiKeyV1 { get; set; }

        /// <summary>
        /// Gets a string containing the PagerDuty account name.
        /// </summary>
        string PagerDutyAccountName { get; set; }

        /// <summary>
        /// Gets a string containing the PagerDuty API key.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        string PagerDutyAPIKey { get; set; }

        /// <summary>
        /// Gets a string containing the PagerDuty Service key.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        string PagerDutyServiceKey { get; set; }
    }
}
