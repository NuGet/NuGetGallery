using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Resolver
{
    class ResolverMetadataClient
    {
        public static async Task<RegistrationInfo> GetTree(HttpClient httpClient, Uri registrationUri, VersionRange range, Func<IDictionary<NuGetVersion, HashSet<string>>, IDictionary<NuGetVersion, HashSet<string>>> filter = null)
        {
            ConcurrentDictionary<Uri, JObject> sessionCache = new ConcurrentDictionary<Uri, JObject>();

            RegistrationInfo registrationInfo = await GetRegistrationInfo(httpClient, registrationUri, range, sessionCache);

            ApplyFilter(registrationInfo, filter);

            await InlineDependencies(httpClient, registrationInfo, sessionCache, filter);

            return registrationInfo;
        }

        static async Task InlineDependencies(HttpClient httpClient, RegistrationInfo registrationInfo, ConcurrentDictionary<Uri, JObject> sessionCache, Func<IDictionary<NuGetVersion, HashSet<string>>, IDictionary<NuGetVersion, HashSet<string>>> filter)
        {
            foreach (PackageInfo packageInfo in registrationInfo.Packages)
            {
                foreach (DependencyGroupInfo dependencyGroupInfo in packageInfo.DependencyGroups)
                {
                    foreach (DependencyInfo dependencyInfo in dependencyGroupInfo.Dependencies)
                    {
                        dependencyInfo.RegistrationInfo = await GetRegistrationInfo(httpClient, dependencyInfo.RegistrationUri, dependencyInfo.Range, sessionCache);
                        
                        ApplyFilter(dependencyInfo.RegistrationInfo, filter);

                        await InlineDependencies(httpClient, dependencyInfo.RegistrationInfo, sessionCache, filter);
                    }
                }
            }
        }

        static void ApplyFilter(RegistrationInfo registrationInfo, Func<IDictionary<NuGetVersion, HashSet<string>>, IDictionary<NuGetVersion, HashSet<string>>> filter)
        {
            IDictionary<NuGetVersion, HashSet<string>> before = new Dictionary<NuGetVersion, HashSet<string>>();

            foreach (PackageInfo packageInfo in registrationInfo.Packages)
            {
                HashSet<string> targetFrameworks = new HashSet<string>();
                foreach (DependencyGroupInfo dependencyGroupInfo in packageInfo.DependencyGroups)
                {
                    targetFrameworks.Add(dependencyGroupInfo.TargetFramework);
                }
                before.Add(packageInfo.Version, targetFrameworks);
            }

            IDictionary<NuGetVersion, HashSet<string>> after = filter(before);

            foreach (PackageInfo packageInfo in registrationInfo.Packages)
            {
                HashSet<string> dependencyGroupsToRetain = after[packageInfo.Version];

                IList<DependencyGroupInfo> dependencyGroupsToRemove = new List<DependencyGroupInfo>();

                foreach (DependencyGroupInfo dependencyGroupInfo in packageInfo.DependencyGroups)
                {
                    if (!dependencyGroupsToRetain.Contains(dependencyGroupInfo.TargetFramework))
                    {
                        dependencyGroupsToRemove.Add(dependencyGroupInfo);
                    }
                }

                foreach (DependencyGroupInfo dependencyGroupInfo in dependencyGroupsToRemove)
                {
                    packageInfo.DependencyGroups.Remove(dependencyGroupInfo);
                }
            }
        }

        static async Task<JObject> LoadResource(HttpClient httpClient, Uri uri, ConcurrentDictionary<Uri, JObject> sessionCache)
        {
            JObject obj;
            if (sessionCache != null && sessionCache.TryGetValue(uri, out obj))
            {
                return obj;
            }

            HttpResponseMessage response = await httpClient.GetAsync(uri);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            obj = JObject.Parse(json);

            if (sessionCache != null)
            {
                sessionCache.TryAdd(uri, obj);
            }

            return obj;
        }

        public static async Task<RegistrationInfo> GetRegistrationInfo(HttpClient httpClient, Uri registrationUri, VersionRange range, ConcurrentDictionary<Uri, JObject> sessionCache = null)
        {
            JObject index = await LoadResource(httpClient, registrationUri, sessionCache);

            if (index == null)
            {
                throw new ArgumentException(registrationUri.AbsoluteUri);
            }

            VersionRange preFilterRange = ResolverUtils.SetIncludePrerelease(range, true);

            IList<Task<JObject>> rangeTasks = new List<Task<JObject>>();

            foreach (JObject item in index["items"])
            {
                NuGetVersion lower = NuGetVersion.Parse(item["lower"].ToString());
                NuGetVersion upper = NuGetVersion.Parse(item["upper"].ToString());

                if (preFilterRange.Satisfies(lower) || preFilterRange.Satisfies(upper))
                {
                    JToken items;
                    if (!item.TryGetValue("items", out items))
                    {
                        Uri rangeUri = item["@id"].ToObject<Uri>();

                        rangeTasks.Add(LoadResource(httpClient, registrationUri, sessionCache));
                    }
                    else
                    {
                        rangeTasks.Add(Task.FromResult(item));
                    }
                }
            }

            await Task.WhenAll(rangeTasks.ToArray());

            RegistrationInfo registrationInfo = new RegistrationInfo();

            registrationInfo.IncludePrerelease = range.IncludePrerelease;

            string id = string.Empty;

            foreach (JObject rangeObj in rangeTasks.Select((t) => t.Result))
            {
                if (rangeObj == null)
                {
                    throw new InvalidDataException(registrationUri.AbsoluteUri);
                }

                foreach (JObject packageObj in rangeObj["items"])
                {
                    JObject catalogEntry = (JObject)packageObj["catalogEntry"];

                    NuGetVersion packageVersion = NuGetVersion.Parse(catalogEntry["version"].ToString());

                    id = catalogEntry["id"].ToString();

                    if (range.Satisfies(packageVersion))
                    {
                        PackageInfo packageInfo = new PackageInfo();
                        packageInfo.Version = packageVersion;
                        packageInfo.PackageContent = new Uri(packageObj["packageContent"].ToString());

                        JArray dependencyGroupsArray = (JArray)catalogEntry["dependencyGroups"];

                        if (dependencyGroupsArray != null)
                        {
                            foreach (JObject dependencyGroupObj in dependencyGroupsArray)
                            {
                                DependencyGroupInfo dependencyGroupInfo = new DependencyGroupInfo();
                                dependencyGroupInfo.TargetFramework = (dependencyGroupObj["targetFramework"] != null) ? dependencyGroupObj["targetFramework"].ToString() : string.Empty;

                                foreach (JObject dependencyObj in dependencyGroupObj["dependencies"])
                                {
                                    DependencyInfo dependencyInfo = new DependencyInfo();
                                    dependencyInfo.Id = dependencyObj["id"].ToString();
                                    dependencyInfo.Range = ResolverUtils.CreateVersionRange((string)dependencyObj["range"], range.IncludePrerelease);
                                    dependencyInfo.RegistrationUri = dependencyObj["registration"].ToObject<Uri>();

                                    dependencyGroupInfo.Dependencies.Add(dependencyInfo);
                                }

                                packageInfo.DependencyGroups.Add(dependencyGroupInfo);
                            }
                        }

                        registrationInfo.Packages.Add(packageInfo);
                    }
                }

                registrationInfo.Id = id;
            }

            return registrationInfo;
        }
    }
}
