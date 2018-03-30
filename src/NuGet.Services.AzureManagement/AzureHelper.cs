// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.AzureManagement
{
    public static class AzureHelper
    {
        public class CloudServiceProperties
        {
            public Uri Uri { get; }
            public int InstanceCount { get; }

            public CloudServiceProperties(Uri uri, int instanceCount)
            {
                Uri = uri;
                InstanceCount = instanceCount;
            }
        }

        public class TrafficManagerProperties
        {
            public string Domain { get; }
            public string Path { get; }
            public IEnumerable<TrafficManagerEndpointProperties> Endpoints { get; }

            public TrafficManagerProperties(
                string domain, 
                string path, 
                IEnumerable<TrafficManagerEndpointProperties> endpoints)
            {
                Domain = domain;
                Path = path;
                Endpoints = endpoints;
            }
        }
        
        public enum TrafficManagerEndpointProbeStatus
        {
            Enabled,
            Disabled
        }

        public enum TrafficManagerEndpointStatus
        {
            CheckingEndpoint,
            Online,
            Degraded,
            Disabled,
            Inactive,
            Stopped
        }

        public class TrafficManagerEndpointProperties
        {
            public string Name { get; }
            public string Target { get; }
            public TrafficManagerEndpointProbeStatus ProbeStatus { get; }
            public TrafficManagerEndpointStatus Status { get; }

            public TrafficManagerEndpointProperties(
                string name, 
                string target, 
                TrafficManagerEndpointProbeStatus probeStatus, 
                TrafficManagerEndpointStatus status)
            {
                Name = name;
                Target = target;
                ProbeStatus = probeStatus;
                Status = status;
            }
        }

        /// <summary>
        /// Parses the result of https://management.azure.com/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.ClassicCompute/domainNames/{2}/slots/{3}?api-version=2016-11-01
        /// </summary>
        public static CloudServiceProperties ParseCloudServiceProperties(string propertiesString)
        {
            try
            {
                var jObject = JObject.Parse(propertiesString);
                var properties = jObject["properties"];

                var uri = new Uri(properties["uri"].ToString());

                // This contains the cscfg format configuration in XML format
                var configuration = properties["configuration"].ToString();

                using (var stringReader = new StringReader(configuration))
                {
                    using (var xmlReader = XmlReader.Create(stringReader))
                    {
                        var xmlConfig = new XmlDocument();
                        xmlConfig.XmlResolver = null;

                        xmlConfig.Load(xmlReader);
                        var instancesElement = xmlConfig.GetElementsByTagName("Instances")[0];
                        var instanceCount = int.Parse(instancesElement.Attributes["count"].InnerText);

                        return new CloudServiceProperties(uri, instanceCount);
                    }
                }
            }
            catch (Exception e)
            {
                throw new AzureManagementException($"Failed to parse cloud service properties string: {propertiesString}", e);
            }
        }

        /// <summary>
        /// Parses the result of https://management.azure.com/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.Network/trafficmanagerprofiles/{2}?api-version=2017-05-01
        /// </summary>
        public static TrafficManagerProperties ParseTrafficManagerProperties(string propertiesString)
        {
            try
            {
                var jObject = JObject.Parse(propertiesString);
                var properties = jObject["properties"];

                var domain = properties["dnsConfig"]["fqdn"].ToString();
                var path = properties["monitorConfig"]["path"].ToString();

                var endpoints = new List<TrafficManagerEndpointProperties>();
                foreach (var endpoint in properties["endpoints"].Children())
                {
                    var name = endpoint["name"].ToString();

                    var endpointProperties = endpoint["properties"];
                    var target = endpointProperties["target"].ToString();
                    var probeStatus = (TrafficManagerEndpointProbeStatus)Enum.Parse(typeof(TrafficManagerEndpointProbeStatus), endpointProperties["endpointStatus"].ToString());
                    var status = (TrafficManagerEndpointStatus)Enum.Parse(typeof(TrafficManagerEndpointStatus), endpointProperties["endpointMonitorStatus"].ToString());

                    endpoints.Add(new TrafficManagerEndpointProperties(name, target, probeStatus, status));
                }

                return new TrafficManagerProperties(domain, path, endpoints);
            }
            catch (Exception e)
            {
                throw new AzureManagementException($"Failed to parse traffic manager properties string: {propertiesString}", e);
            }
        }
    }
}
