using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Resolver.Metadata;
using Resolver.Resolver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NuGet3.Client.Core
{
    public static class Commands
    {
        public static async Task Install(PackageSources sources, string packageId)
        {
            var storage = new ServiceClient(sources);

            // 1. Load package3.config
            JObject packageConfig;
            try
            {
                string packageConfigJson = File.ReadAllText("packages.config.json");
                packageConfig = PackageConfig.Load(packageConfigJson);

            }
            catch (FileNotFoundException)
            {
                packageConfig = JObject.Parse(@"{ ""packages"": [], ""@context"": { ""@vocab"": ""http://schema.nuget.org/packagesConfig#"",
    ""pc"": ""http://schema.nuget.org/packagesConfig#"",
    ""package"": ""@id"",
    ""packages"": {""@id"": ""pc:package"", ""@container"": ""@set"" },
    ""metadataSources"": {""@id"": ""pc:metadataSource"", ""@container"": ""@set"", ""@type"":  ""@id"" },
    ""downloadLocations"": {""@id"": ""pc:downloadLocation"", ""@container"": ""@set"", ""@type"": ""@id"" } } }
");
                File.WriteAllText("packages.config.json", packageConfig.ToString());
            }

            // 3. Resolve dependencies.
            List<string> installed = new List<string> { packageId };
            foreach (JObject package in packageConfig["packages"])
            {
                if (package["explicitlyInstalled"].ToObject<bool>())
                {
                    installed.Add(package["id"].ToObject<string>());
                }
            }

            IGallery gallery = storage.GetResolverClient().Gallery;

            IList<Package> solution = await Runner.ResolveDependencies(gallery, installed);

            // 4. Update packages.config.json.
            HttpClient hc = new HttpClient();

            foreach (Package item in solution)
            {
                string nupkgPath = (string)item.PackageJson["nupkgUrl"];

                string id = item.Id;
                string version = item.Version.ToString();
                string url = item.PackageJson["url"].ToObject<string>();

                bool alreadyPresent = false;
                foreach (JObject package in packageConfig["packages"])
                {
                    if (string.Compare(package["id"].ToObject<string>(), id, ignoreCase: true) == 0)
                    {
                        alreadyPresent = true;
                        package["package"] = url;
                        package["version"] = version;
                        package["explicitlyInstalled"] = installed.Where(i => string.Compare(id, i, ignoreCase: true) == 0).Any();
                        package["packageRestoreHints"]["downloadLocations"] = new JArray { nupkgPath };
                    }
                }
                if (!alreadyPresent)
                {
                    ((JArray)packageConfig["packages"]).Add(new JObject {
                    { "package", url},
                    { "id", id},
                    {"version", version},
                    {"explicitlyInstalled", installed.Where(i => string.Compare(id, i, ignoreCase: true) == 0).Any() },
                    {"packageRestoreHints", new JObject {
                        {"metadataSources", new JArray {"https://api.nuget.org/ver3/"} },
                        {"downloadLocations", new JArray { nupkgPath } },
                    }}});
                }
            }
            File.WriteAllText("packages.config.json", packageConfig.ToString());

            // 5. Download the dependencies.
            await Restore(sources);
        }

        public static async Task Restore(PackageSources sources)
        {
            var serviceClient = new ServiceClient(sources);

            if (!Directory.Exists(".nuget/packages"))
            {
                Directory.CreateDirectory(".nuget/packages");
            }

            JObject packageConfig;
            try
            {
                string packageConfigJson = File.ReadAllText("packages.config.json");
                packageConfig = PackageConfig.Load(packageConfigJson);
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("No packages.config.json file.");
                return;
            }

            HttpClient hc = new HttpClient();
            foreach (JObject package in packageConfig["packages"])
            {
                string id = package["id"].ToObject<string>().ToLower();
                string version = package["version"].ToObject<string>();

                string downloadPath = package["packageRestoreHints"]["downloadLocations"][0].ToObject<string>();

                if (File.Exists(".nuget/packages/" + id + "." + version + ".nupkg"))
                {
                    continue;
                }

                Console.WriteLine("Downloading {0}...", downloadPath);

                using (Stream nupkg = await hc.GetStreamAsync(package["packageRestoreHints"]["downloadLocations"][0].ToObject<string>()))
                using (FileStream download = File.Create(".nuget/packages/" + id + "." + version + ".nupkg"))
                {
                    nupkg.CopyTo(download);
                }
            }
        }
    }
}
