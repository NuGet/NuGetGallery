using Lucene.Net.Documents;
using Lucene.Net.Index;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using System;
using System.Linq;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public class PackageVersions
    {
        IndexReader _reader;
        IDictionary<string, List<NuGetVersion>> _registrations;

        public PackageVersions(IndexReader reader)
        {
            _reader = reader;
            _registrations = new Dictionary<string, List<NuGetVersion>>();

            for (int i = 0; i < reader.MaxDoc; i++)
            {
                if (reader.IsDeleted(i))
                {
                    continue;
                }

                Document document = reader[i];

                if (document.GetValues("@type").Contains("http://schema.nuget.org/schema#Package"))
                {
                    string id = document.Get("Id").ToLowerInvariant();
                    NuGetVersion version = NuGetVersion.Parse(document.Get("Version"));

                    List<NuGetVersion> versions;
                    if (!_registrations.TryGetValue(id, out versions))
                    {
                        versions = new List<NuGetVersion>();
                        _registrations.Add(id, versions);
                    }

                    versions.Add(version);
                }
            }

            foreach (List<NuGetVersion> values in _registrations.Values)
            {
                values.Sort();
            }
        }

        public JArray[] CreateVersionsLookUp(IDictionary<string, IDictionary<string, int>> downloadLookup, Uri registrationBaseAddress)
        {
            IDictionary<string, JArray> versionsById = new Dictionary<string, JArray>();

            foreach (KeyValuePair<string, List<NuGetVersion>> registration in _registrations)
            {
                IDictionary<string, int> downloadsByVersion = null;
                if (downloadLookup != null)
                {
                    downloadLookup.TryGetValue(registration.Key, out downloadsByVersion);
                }

                JArray versions = new JArray();

                foreach (NuGetVersion version in _registrations[registration.Key])
                {
                    string versionStr = version.ToNormalizedString();

                    JObject versionObj = new JObject();
                    versionObj.Add("version", versionStr);

                    int downloads = 0;
                    if (downloadsByVersion != null)
                    {
                        downloadsByVersion.TryGetValue(versionStr, out downloads);
                    }
                    versionObj.Add("downloads", downloads);

                    Uri versionUri = new Uri(registrationBaseAddress, string.Format("{0}/{1}.json", registration.Key, version).ToLowerInvariant());
                    versionObj.Add("@id", versionUri.AbsoluteUri);

                    versions.Add(versionObj);
                }

                versionsById.Add(registration.Key, versions);
            }

            return CreateVersionsByDoc(versionsById);
        }

        public JArray[] CreateVersionListsLookUp()
        {
            IDictionary<string, JArray> versionsById = new Dictionary<string, JArray>();

            foreach (KeyValuePair<string, List<NuGetVersion>> registration in _registrations)
            {
                JArray versions = new JArray();

                foreach (NuGetVersion version in _registrations[registration.Key])
                {
                    versions.Add(version.ToNormalizedString());
                }

                versionsById.Add(registration.Key, versions);
            }

            return CreateVersionsByDoc(versionsById);
        }

        JArray[] CreateVersionsByDoc(IDictionary<string, JArray> versionsById)
        {
            JArray[] versionsByDoc = new JArray[_reader.MaxDoc];

            for (int i = 0; i < _reader.MaxDoc; i++)
            {
                if (_reader.IsDeleted(i))
                {
                    continue;
                }

                Document document = _reader[i];

                if (document.GetValues("@type").Contains("http://schema.nuget.org/schema#Package"))
                {
                    string id = document.Get("Id").ToLowerInvariant();
                    versionsByDoc[i] = versionsById[id];
                }
                else
                {
                    versionsByDoc[i] = new JArray();
                }
            }

            return versionsByDoc;
        }
    }
}
