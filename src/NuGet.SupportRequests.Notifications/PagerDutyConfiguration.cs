// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace NuGet.SupportRequests.Notifications
{
    internal class PagerDutyConfiguration
    {
        private const string _onCallV1EndpointFormat = "https://{0}.pagerduty.com/api/v1/users/on_call";

        public PagerDutyConfiguration(string accountName, string apiKey)
        {
            if (string.IsNullOrEmpty(accountName))
            {
                throw new ArgumentNullException(nameof(accountName));
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentNullException(nameof(apiKey));
            }

            AccountName = accountName;
            ApiKey = apiKey;
        }

        public string ApiKey { get; }
        public string AccountName { get; }

        public string GetOnCallUrl()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                _onCallV1EndpointFormat,
                AccountName);
        }
    }
}
