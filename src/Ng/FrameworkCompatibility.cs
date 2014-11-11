using Newtonsoft.Json.Linq;
using NuGet;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

namespace Ng
{
    public class FrameworkCompatibility
    {
        public static void Run(string[] args)
        {
            string queryJsonString = File.ReadAllText("projectframeworks.v1.json");
            JObject queryJson = JObject.Parse(queryJsonString);

            JObject compat = new JObject();

            IEnumerable<string> fwkNames = File.ReadLines("frameworks.txt").Select(x => x.Split(":".ToCharArray())[1].TrimStart()).OrderBy(x => x).Distinct().ToList();

            foreach (JToken queryFwkToken in queryJson["data"])
            {
                string queryFwk = (string)queryFwkToken;
                if (((IDictionary<string, JToken>)compat).ContainsKey(queryFwk)) continue;
                JObject compatList = new JObject();
                compat[queryFwk] = compatList;

                foreach (string fwk in fwkNames)
                {
                    if (VersionUtility.IsCompatible(new FrameworkName(queryFwk), new List<FrameworkName> { new FrameworkName(fwk) }))
                    {
                        compatList[fwk] = true;
                    }
                }
            }

            JObject wrapper = new JObject();

            wrapper["info"] = "Built from projectframeworks.v1.json and the output of the search index builder.";
            wrapper["data"] = compat;

            File.WriteAllText("frameworkCompat.v1.json", wrapper.ToString());

        }
    }
}
