using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Resolver.Metadata
{
    public class RemoteGallery : IGallery
    {
        string _source;

        IDictionary<string, Task<Registration>> _cache;

        public RemoteGallery(string source)
        {
            _source = source;
            _cache = new Dictionary<string, Task<Registration>>();
        }

        public Task<Registration> GetRegistration(string registrationId)
        {
            Task<Registration> registration;
            lock (_cache)
            {
                if (!_cache.TryGetValue(registrationId, out registration))
                {
                    Console.WriteLine("Not found in cache {0}", registrationId);
                    registration = InnerGetRegistration(registrationId);
                    _cache.Add(registrationId, registration);
                }
            }
            Console.WriteLine("Found in cache {0}", registrationId);
            return registration;
        }


        public async Task<Registration> InnerGetRegistration(string registrationId)
        {
            string address = (_source.Trim('/') + '/' + registrationId + ".json").ToLowerInvariant();

            Console.WriteLine(address);

            HttpClient client = new HttpClient();

            HttpResponseMessage response = await client.GetAsync(address);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                string json = await response.Content.ReadAsStringAsync();

                JObject jobjectRegistration = JObject.Parse(json);
                string id = jobjectRegistration["id"].ToObject<string>();

                Registration registration = new Registration();
                registration.Id = id;

                JArray jarrayPackages = (JArray)jobjectRegistration["package"];

                foreach (JObject jobjectPackage in jarrayPackages)
                {
                    string version = jobjectPackage["version"].ToObject<string>();

                    Package package = new Package(id, version);
                    registration.Packages.Add(package);

                    JToken jtokenDependencies;
                    if (jobjectPackage.TryGetValue("dependencies", out jtokenDependencies))
                    {
                        JObject dependencies = (JObject)jtokenDependencies;

                        JArray jarrayGroups = (JArray)dependencies["group"];

                        foreach (JObject jobjectGroup in jarrayGroups)
                        {
                            string groupTargetFramework = "all";
                            JToken jtokenTargetFramework;
                            if (jobjectGroup.TryGetValue("targetFramework", out jtokenTargetFramework))
                            {
                                groupTargetFramework = jtokenTargetFramework.ToObject<string>();
                            }

                            Group group = new Group(groupTargetFramework);

                            package.DependencyGroups.Add(group);

                            JToken properties;
                            if (jobjectGroup.TryGetValue("property", out properties))
                            {
                                foreach (JToken property in properties)
                                {
                                    group.Properties.Add((string)property["name"], (string)property["value"]);
                                }
                            }

                            JArray jarrayDependency = (JArray)jobjectGroup["dependency"];

                            foreach (JObject jobjectDependencyPart in jarrayDependency)
                            {
                                string dependencyId = jobjectDependencyPart["id"].ToObject<string>();

                                string dependencyRange = "LATEST";
                                JToken jtokenRange;
                                if (jobjectDependencyPart.TryGetValue("range", out jtokenRange))
                                {
                                    dependencyRange = jtokenRange.ToObject<string>();
                                }

                                Dependency dependency = new Dependency(dependencyId, dependencyRange);

                                JToken jtokenProperties;
                                if (jobjectDependencyPart.TryGetValue("property", out jtokenProperties))
                                {
                                    foreach (JToken property in jtokenProperties)
                                    {
                                        dependency.Properties.Add((string)property["name"], (string)property["value"]);
                                    }
                                }

                                group.Dependencies.Add(dependency);
                            }
                        }
                    }
                }

                return registration;
            }

            throw new Exception(string.Format("unable to load: {0}", registrationId));
        }
    }
}
