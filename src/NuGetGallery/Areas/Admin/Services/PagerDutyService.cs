// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGetGallery.Configuration;

namespace NuGetGallery.Areas.Admin
{
    public class PagerDutyService : IMonitoringService
    {
        public async Task<string> GetPrimaryOnCall(IAppConfiguration appConfiguration)
        {
            var username = string.Empty;
            try
            {
                var pagerDutyApiKey = appConfiguration.PagerDutyAPIKey;
                var pagerDutyOnCallUrl = appConfiguration.PagerDutyOnCallURL;

                string response;
                using (var httpClient = new HttpClient()) //("URL/create_event.json");
                {
                    var token = "Token token=" + pagerDutyApiKey;
                    httpClient.DefaultRequestHeaders.Add("Authorization", token);

                    response = await httpClient.GetStringAsync(pagerDutyOnCallUrl);
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

                            //Find the primary that is not nugetcore
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

        public async Task TriggerIncident(IAppConfiguration appConfiguration, string errorMessage)
        {
            try
            {
                var pagerDutyApiKey = appConfiguration.PagerDutyAPIKey;
                var pagerDutyServiceKey = appConfiguration.PagerDutyServiceKey;
                var pagerDutyIncidentTriggerUrl = appConfiguration.PagerDutyIncidentTriggerURL;

                using (var httpClient = new HttpClient())
                {
                    var token = "Token token=" + pagerDutyApiKey;
                    httpClient.DefaultRequestHeaders.Add("Authorization", token);

                    var obj = new JObject
                    {
                        { "service_key", pagerDutyServiceKey },
                        { "event_type", "trigger" },
                        { "description", errorMessage }
                    };

                    var content = new StringContent(obj.ToString());
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                    var response = await httpClient.PostAsync(pagerDutyIncidentTriggerUrl, content);
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
