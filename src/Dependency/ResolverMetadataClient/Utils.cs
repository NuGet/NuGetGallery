using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Resolver
{
    public static class ResolverUtils
    {
        public static VersionRange CreateVersionRange(string stringToParse, bool includePrerelease)
        {
            VersionRange range = VersionRange.Parse(string.IsNullOrEmpty(stringToParse) ? "[0.0.0-alpha,)" : stringToParse);
            return new VersionRange(range.MinVersion, range.IsMinInclusive, range.MaxVersion, range.IsMaxInclusive, includePrerelease);
        }

        public static async Task<JObject> GetJObjectAsync(HttpClient httpClient, Uri registrationUri)
        {
            string json = await httpClient.GetStringAsync(registrationUri);
            return JObject.Parse(json);
        }

        public static VersionRange SetIncludePrerelease(VersionRange range, bool includePrerelease)
        {
            return new VersionRange(range.MinVersion, range.IsMinInclusive, range.MaxVersion, range.IsMaxInclusive, includePrerelease);
        }

        public static string Indent(int depth)
        {
            return new string(Enumerable.Repeat(' ', depth).ToArray());
        }

        public static void Print(RegistrationInfo registrationInfo)
        {
            foreach (PackageInfo packageInfo in registrationInfo.Packages)
            {
                Console.WriteLine(packageInfo.Version);

                foreach (DependencyGroupInfo dependencyGroupInfo in packageInfo.DependencyGroups)
                {
                    Console.WriteLine("  {0}", dependencyGroupInfo.TargetFramework ?? "DEFAULT");

                    foreach (DependencyInfo dependencyInfo in dependencyGroupInfo.Dependencies)
                    {
                        Console.WriteLine("    {0}", dependencyInfo.Id);
                        Console.WriteLine("    {0}", dependencyInfo.Range != null ? dependencyInfo.Range.ToString() : "LATEST");
                        Console.WriteLine("    {0}", dependencyInfo.RegistrationUri);
                    }
                }
            }
        }
    }
}
