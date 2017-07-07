// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Xml;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.AzureManagement
{
    public static class AzureHelper
    {
        public class CloudService
        {
            public Uri Uri { get; set; }
            public int InstanceCount { get; set; }
        }

        /// <summary>
        /// Parses the result of https://management.azure.com/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.ClassicCompute/domainNames/{2}/slots/{3}?api-version=2016-11-01
        /// </summary>
        public static CloudService ParseCloudServiceProperties(string propertiesString)
        {
            try
            {
                var cloudService = new CloudService();

                var jObject = JObject.Parse(propertiesString);
                cloudService.Uri = new Uri(jObject["properties"]["uri"].ToString());

                // This contains the cscfg format configuration in XML format
                var configuration = jObject["properties"]["configuration"].ToString();

                var xmlConfig = new XmlDocument();
                xmlConfig.LoadXml(configuration);
                var instancesElement = xmlConfig.GetElementsByTagName("Instances")[0];
                cloudService.InstanceCount = int.Parse(instancesElement.Attributes["count"].InnerText);

                return cloudService;
            }
            catch (Exception e)
            {
                throw new AzureManagementException($"Failed to parse cloud service properties string: {propertiesString}", e);
            }
        }
    }
}
