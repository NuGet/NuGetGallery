// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.SupportRequests.Notifications
{
    public class InitializationConfiguration
    {
        /// <summary>
        /// Obsolete: replace with IcM configuration
        /// </summary>
        public string PagerDutyAccountName { get; set; }

        /// <summary>
        /// Obsolete: replace with IcM configuration
        /// </summary>
        public string PagerDutyApiKey { get; set; }

        /// <summary>
        /// SMTP configuration.
        /// </summary>
        public string SmtpUri { get; set; }

        /// <summary>
        /// Email address to which the weekly report is sent.
        /// </summary>
        public string TargetEmailAddress { get; set; }
    }
}
