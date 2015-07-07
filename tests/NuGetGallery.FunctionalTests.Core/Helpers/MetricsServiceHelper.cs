// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests
{
    public class MetricsServiceHelper
        : HelperBase
    {
        public const string IdKey = "id";
        public const string VersionKey = "version";
        public const string IpAddressKey = "ipAddress";
        public const string UserAgentKey = "userAgent";
        public const string OperationKey = "operation";
        public const string DependentPackageKey = "dependentPackage";
        public const string ProjectGuidsKey = "projectGuids";
        public const string HttpPost = "POST";
        public const string MetricsDownloadEventMethod = "/DownloadEvent";
        public const string ContentTypeJson = "application/json";
        public const string MetricsServiceUri = "http://api-metrics.int.nugettest.org";

        public MetricsServiceHelper()
            : this(ConsoleTestOutputHelper.New)
        {
        }

        public MetricsServiceHelper(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        public async Task<bool> TryHitMetricsEndPoint(string id, string version, string ipAddress, string userAgent, string operation, string dependentPackage, string projectGuids)
        {
            var jObject = GetJObject(id, version, ipAddress, userAgent, operation, dependentPackage, projectGuids);
            var result = await TryHitMetricsEndPoint(jObject);
            return result;
        }

        public async Task<bool> TryHitMetricsEndPoint(JObject jObject)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var requestUri = new Uri(MetricsServiceUri + MetricsDownloadEventMethod);
                    var content = new StringContent(jObject.ToString(), Encoding.UTF8, ContentTypeJson);
                    var response = await httpClient.PostAsync(requestUri, content);

                    //print the header
                    WriteLine("HTTP status code : {0}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.Accepted)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch (HttpRequestException hre)
            {
                WriteLine("Exception : {0}", hre.Message);
                return false;
            }
        }

        private static JObject GetJObject(string id, string version, string ipAddress, string userAgent, string operation, string dependentPackage, string projectGuids)
        {
            var jObject = new JObject();
            jObject.Add(IdKey, id);
            jObject.Add(VersionKey, version);
            if (!string.IsNullOrEmpty(ipAddress)) jObject.Add(IpAddressKey, ipAddress);
            if (!string.IsNullOrEmpty(userAgent)) jObject.Add(UserAgentKey, userAgent);
            if (!string.IsNullOrEmpty(operation)) jObject.Add(OperationKey, operation);
            if (!string.IsNullOrEmpty(dependentPackage)) jObject.Add(DependentPackageKey, dependentPackage);
            if (!string.IsNullOrEmpty(projectGuids)) jObject.Add(ProjectGuidsKey, projectGuids);

            return jObject;
        }
    }
}
