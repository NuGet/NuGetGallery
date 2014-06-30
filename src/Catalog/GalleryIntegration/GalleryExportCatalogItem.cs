using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.GalleryIntegration
{
    public class GalleryExportCatalogItem : CatalogItem
    {
        internal static readonly Uri GalleryKeyPredicateUri = new Uri("http://nuget.org/gallery#key");
        internal static readonly Uri GalleryChecksumPredicateUri = new Uri("http://nuget.org/gallery#checksum");
        internal static readonly Uri IntegerDatatypeUri = new Uri("http://www.w3.org/2001/XMLSchema#integer");
        internal static readonly Uri StringDatatypeUri = new Uri("http://www.w3.org/2001/XMLSchema#string");

        GalleryExportPackage _export;
        string _identity;

        public GalleryExportCatalogItem(GalleryExportPackage export)
        {
            _export = export;
            _identity = (export.Id + "." + export.Package["Version"]).ToLowerInvariant();
        }

        public override StorageContent CreateContent(CatalogContext context)
        {
            string resourceUri = GetBaseAddress() + GetRelativeAddress();
            JObject obj = CreateContent(resourceUri, _export, GetTimeStamp(), GetCommitId());
            JObject frame = context.GetJsonLdContext("context.Package.json", GetItemType());
            obj.Add("@context", frame["@context"]);

            StorageContent content = new StringStorageContent(obj.ToString(), "application/json");

            return content;
        }

        public override Uri GetItemType()
        {
            return Constants.Package;
        }

        public override IGraph CreatePageContent(CatalogContext context)
        {
            Uri resourceUri = new Uri(GetBaseAddress() + GetRelativeAddress());

            Graph graph = new Graph();

            INode subject = graph.CreateUriNode(resourceUri);
            INode galleryKeyPredicate = graph.CreateUriNode(GalleryKeyPredicateUri);
            INode galleryChecksumPredicate = graph.CreateUriNode(GalleryChecksumPredicateUri);

            string key = _export.Package.Value<string>("Key");
            string checksum = _export.Package.Value<string>("DatabaseChecksum");

            graph.Assert(subject, galleryKeyPredicate, graph.CreateLiteralNode(key, IntegerDatatypeUri));
            graph.Assert(subject, galleryChecksumPredicate, graph.CreateLiteralNode(checksum));

            return graph;
        }

        protected override string GetItemIdentity()
        {
            return _identity;
        }

        static JObject CreateContent(string resourceUri, GalleryExportPackage export, DateTime timeStamp, Guid commitId)
        {
            IDictionary<string, string> Lookup = new Dictionary<string, string>
            {
                { "Title", "title" },
                { "Version", "version" },
                { "Description", "description" },
                { "Summary", "summary" },
                { "Authors", "authors" },
                { "LicenseUrl", "licenseUrl" },
                { "ProjectUrl", "projectUrl" },
                { "IconUrl", "iconUrl" },
                { "RequireLicenseAcceptance", "requireLicenseAcceptance"},
                { "Language", "language" },
                { "ReleaseNotes", "releaseNotes"}
            };

            JObject obj = new JObject();

            obj.Add("http://nuget.org/gallery#key", export.Package["Key"].ToObject<int>());
            obj.Add("http://nuget.org/gallery#checksum", export.Package["DatabaseChecksum"].ToObject<string>());

            obj.Add("url", resourceUri);

            obj.Add("@type", "Package");

            obj.Add("http://nuget.org/catalog#commitId", commitId);

            obj.Add("http://nuget.org/catalog#timeStamp", 
                new JObject
                { 
                    { "@type", "http://www.w3.org/2001/XMLSchema#dateTime" },
                    { "@value", timeStamp.ToString() }
                });

            obj.Add("id", export.Id);

            foreach (JProperty property in export.Package.Properties())
            {
                if (property.Name == "Tags")
                {
                    char[] trimChar = { ',', ' ', '\t', '|', ';' };

                    IEnumerable<string> fields = property.Value.ToString()
                        .Split(trimChar)
                        .Select((w) => w.Trim(trimChar))
                        .Where((w) => w.Length > 0);

                    JArray tagArray = new JArray();
                    foreach (string field in fields)
                    {
                        tagArray.Add(field);
                    }
                    obj.Add("tag", tagArray);
                }
                else
                {
                    string name;
                    if (Lookup.TryGetValue(property.Name, out name))
                    {
                        obj.Add(name, property.Value);
                    }
                }
            }

            if (export.Dependencies != null)
            {
                string dependenciesUri = resourceUri + "#dependencies";

                JObject dependenciesObj = new JObject();

                dependenciesObj.Add("url", dependenciesUri);

                JArray dependencyGroups = new JArray();
                foreach (IGrouping<JToken, JObject> group in export.Dependencies.GroupBy(d => d["TargetFramework"]))
                {
                    string targetFramework = group.Key.ToString();

                    string dependencyGroupUri = dependenciesUri + "/group";

                    JObject dependencyGroup = new JObject();

                    if (targetFramework != "")
                    {
                        dependencyGroup.Add("targetFramework", targetFramework);
                        dependencyGroupUri = dependencyGroupUri + "/" + targetFramework.ToLowerInvariant();
                    }

                    dependencyGroup.Add("url", dependencyGroupUri);

                    JArray dependencyGroupDependencies = new JArray();

                    foreach (JObject value in group)
                    {
                        JObject dependencyGroupDependency = new JObject();

                        string id = value["Id"].ToString().ToLowerInvariant();

                        string dependencyGroupDependencyUri = dependencyGroupUri + "/" + id;

                        dependencyGroupDependency.Add("url", dependencyGroupDependencyUri);
                        dependencyGroupDependency.Add("id", id);
                        dependencyGroupDependency.Add("range", value["VersionSpec"].ToString());

                        dependencyGroupDependencies.Add(dependencyGroupDependency);
                    }

                    dependencyGroup.Add("dependency", dependencyGroupDependencies);

                    dependencyGroups.Add(dependencyGroup);
                }

                dependenciesObj.Add("group", dependencyGroups);

                obj.Add("dependencies", dependenciesObj);
            }

            if (export.TargetFrameworks != null)
            {
                JArray array = new JArray();
                foreach (string targetFramework in export.TargetFrameworks)
                {
                    array.Add(targetFramework);
                }
                obj.Add("targetFramework", array);
            }

            return obj;
        }
    }
}
