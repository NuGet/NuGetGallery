// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Jobs.Configuration;

namespace NuGet.SupportRequests.Notifications
{
    public class InitializationConfiguration : MessageServiceConfiguration
    {
        /// <summary>
        /// Connection string to email publisher job's service bus.
        /// </summary>
        public string EmailPublisherConnectionString { get; set; }

        /// <summary>
        /// Name of the email publisher job's service bus topic.
        /// </summary>
        public string EmailPublisherTopicName { get; set; }

        /// <summary>
        /// Obsolete: replace with incident API configuration
        /// </summary>
        public string PagerDutyAccountName { get; set; }

        /// <summary>
        /// Obsolete: replace with incident API configuration
        /// </summary>
        public string PagerDutyApiKey { get; set; }

        /// <summary>
        /// Email address to which the weekly report is sent.
        /// </summary>
        public string TargetEmailAddress { get; set; }
    }
}
