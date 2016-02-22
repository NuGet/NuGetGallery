// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGetGallery.Areas.Admin
{
    // todo: move this out of the gallery code base...
    internal sealed class PagerDutyClient
    {
        private const string _triggerIncidentUrl = "https://events.pagerduty.com/generic/2010-04-15/create_event.json";
        private readonly string _apiKey;
        private readonly string _serviceKey;
        private readonly string _onCallUrl;

        internal PagerDutyClient(string accountName, string apiKey, string serviceKey)
        {
            _apiKey = apiKey;
            _serviceKey = serviceKey;

            // Configure defaults
            _onCallUrl = string.Format(CultureInfo.InvariantCulture, "https://{0}.pagerduty.com/api/v1/users/on_call", accountName);
        }

        public async Task<string> GetPrimaryOnCallAsync()
        {
            var username = string.Empty;
            try
            {
                string response;
                using (var httpClient = new HttpClient())
                {
                    var token = "Token token=" + _apiKey;
                    httpClient.DefaultRequestHeaders.Add("Authorization", token);

                    response = await httpClient.GetStringAsync(_onCallUrl);
                }


                if (!string.IsNullOrEmpty(response))
                {
                    var root = JObject.Parse(response);
                    var users = (JArray)root["users"];

                    foreach (var item in users)
                    {
                        var onCall = item["on_call"][0];
                        if (Convert.ToInt32(onCall["level"], CultureInfo.InvariantCulture) == 1)
                        {
                            var email = item["email"].ToString();
                            var length = email.IndexOf("@", 0, StringComparison.OrdinalIgnoreCase);
                            username = email.Substring(0, length);

                            // Find the primary that is not nugetcore
                            if (!username.Equals("nugetcore", StringComparison.OrdinalIgnoreCase))
                            {
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                QuietLog.LogHandledException(e);
            }
            return username;
        }

        public async Task TriggerIncidentAsync(string errorMessage)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var token = "Token token=" + _apiKey;
                    httpClient.DefaultRequestHeaders.Add("Authorization", token);

                    var obj = new JObject
                    {
                        { "service_key", _serviceKey },
                        { "event_type", "trigger" },
                        { "description", errorMessage }
                    };

                    var content = new StringContent(obj.ToString());
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                    var response = await httpClient.PostAsync(_triggerIncidentUrl, content);
                    response.EnsureSuccessStatusCode();
                }
            }
            catch (Exception e)
            {
                QuietLog.LogHandledException(e);
            }
        }
    }
}
