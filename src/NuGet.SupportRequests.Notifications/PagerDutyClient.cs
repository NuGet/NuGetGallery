// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGet.SupportRequests.Notifications
{
    internal sealed class PagerDutyClient
    {
        private readonly PagerDutyConfiguration _pagerDutyConfiguration;
        private const string _pagerDutyPolicyId = "PQP8V6O";

        internal PagerDutyClient(PagerDutyConfiguration pagerDutyConfiguration)
        {
            _pagerDutyConfiguration = pagerDutyConfiguration;
        }

        public async Task<string> GetPrimaryOnCallAsync()
        {
            var username = string.Empty;

            string response;
            using (var httpClient = new HttpClient())
            {
                var token = "Token token=" + _pagerDutyConfiguration.ApiKey;
                httpClient.DefaultRequestHeaders.Add("Authorization", token);

                response = await httpClient.GetStringAsync(_pagerDutyConfiguration.GetOnCallUrl());
            }

            if (!string.IsNullOrEmpty(response))
            {
                username = GetEmailAliasFromOnCallUser(response, _pagerDutyPolicyId);
            }

            return username;
        }

        internal static string GetEmailAliasFromOnCallUser(string response, string policyId)
        {
            var username = string.Empty;

            var root = JObject.Parse(response);
            var users = (JArray)root["users"];

            foreach (var item in users)
            {
                foreach (var onCall in item["on_call"])
                {
                    if (Convert.ToInt32(onCall["level"], CultureInfo.InvariantCulture) == 1)
                    {
                        var escalationPolicyId = onCall["escalation_policy"]["id"].Value<string>();
                        if (string.Equals(escalationPolicyId, policyId, StringComparison.Ordinal))
                        {
                            var email = item["email"].ToString();
                            var length = email.IndexOf("@", 0, StringComparison.OrdinalIgnoreCase);
                            var alias = email.Substring(0, length);

                            // Find the primary that is not nugetcore
                            if (!alias.Equals("nugetcore", StringComparison.OrdinalIgnoreCase))
                            {
                                username = alias;
                                break;
                            }
                        }
                    }
                }
            }

            return username;
        }
    }
}
