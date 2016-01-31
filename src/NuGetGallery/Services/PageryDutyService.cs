// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class PagerDutyService : IMonitoringService
    {
        public PagerDutyService()
        {
            
        }

        public async Task<string> GetPrimaryOnCall(IAppConfiguration _config)
        {
            var returnVal = string.Empty;
            try
            {
                var pagerDutyAPIKey = _config.PagerDutyAPIKey;
                var pagerDutyOnCallURL = _config.PagerDutyOnCallURL;

                var httpClient = new HttpClient(); //("URL/create_event.json");

                var _token = "Token token=" + pagerDutyAPIKey;
                httpClient.DefaultRequestHeaders.Add("Authorization", _token);

                var response = await httpClient.GetStringAsync(pagerDutyOnCallURL);

                if (!String.IsNullOrEmpty(response))
                {
                    var root = JObject.Parse(response);
                    var users = (JArray)root["users"];

                    foreach (var item in users)
                    {
                        var on_call = item["on_call"][0];
                        if (Convert.ToInt32(on_call["level"], CultureInfo.InvariantCulture) == 1)
                        {
                            var email = item["email"].ToString();
                            var length = email.IndexOf("@", 0, StringComparison.OrdinalIgnoreCase);
                            returnVal = email.Substring(0, length);
                            //Find the primary that is not nugetcore
                            if (!returnVal.Equals("nugetcore", StringComparison.OrdinalIgnoreCase))
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
            return returnVal;
        }

        public async Task TriggerAPagerDutyIncident(IAppConfiguration _config, string errorMessage)
        {
            try
            {
                var pagerDutyAPIKey = _config.PagerDutyAPIKey;
                var pagerDutyServiceKey = _config.PagerDutyServiceKey;
                var pagerDutyIncidentTriggerURL = _config.PagerDutyIncidentTriggerURL;

                var httpClient = new HttpClient();

                var _token = "Token token=" + pagerDutyAPIKey;
                httpClient.DefaultRequestHeaders.Add("Authorization", _token);

                var obj = new JObject
                {
                    { "service_key", pagerDutyServiceKey },
                    { "event_type", "trigger" },
                    { "description", errorMessage }
                };

                var content = new StringContent(obj.ToString());
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var response = await httpClient.PostAsync(pagerDutyIncidentTriggerURL, content);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                QuietLog.LogHandledException(e);

            }
        }
    }
}
