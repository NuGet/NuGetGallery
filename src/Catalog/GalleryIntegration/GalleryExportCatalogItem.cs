using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;
using Newtonsoft.Json;

namespace NuGet.Services.Metadata.Catalog.GalleryIntegration
{
    public class GalleryExportCatalogItem : CatalogItem
    {
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
            return Schema.DataTypes.Package;
        }

        public override IGraph CreatePageContent(CatalogContext context)
        {
            Uri resourceUri = new Uri(GetBaseAddress() + GetRelativeAddress());

            Graph graph = new Graph();

            INode subject = graph.CreateUriNode(resourceUri);
            INode galleryKeyPredicate = graph.CreateUriNode(Schema.Predicates.GalleryKey);
            INode galleryChecksumPredicate = graph.CreateUriNode(Schema.Predicates.GalleryChecksum);
            INode idPredicate = graph.CreateUriNode(Schema.Predicates.Id);
            INode versionPredicate = graph.CreateUriNode(Schema.Predicates.Version);

            string key = _export.Package.Value<string>("Key");
            string checksum = _export.Package.Value<string>("DatabaseChecksum");
            string id = _export.Id;
            string version = _export.Package.Value<string>("Version");

            graph.Assert(subject, galleryKeyPredicate, graph.CreateLiteralNode(key, Schema.DataTypes.Integer));
            graph.Assert(subject, galleryChecksumPredicate, graph.CreateLiteralNode(checksum));
            graph.Assert(subject, idPredicate, graph.CreateLiteralNode(id));
            graph.Assert(subject, versionPredicate, graph.CreateLiteralNode(version));

            return graph;
        }

        protected override string GetItemIdentity()
        {
            return _identity;
        }

        private static readonly IDictionary<string, string> FieldMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "NormalizedVersion", "version" },
            { "FlattenedAuthors", "authors" },
            { "Copyright", "copyright" },
            { "Created", "created" },
            { "Description", "description" },
            { "IconUrl", "iconUrl" },
            { "IsLatest", "isLatest" },
            { "IsLatestStable", "isLatestStable" },
            { "IsPrerelease", "isPrerelease" },
            { "Language", "language" },
            { "Published", "published" },
            { "LastEdited", "lastEdited" },
            { "PackageHash", "packageHash" },
            { "PackageHashAlgorithm", "packageHashAlgorithm" },
            { "PackageSize", "packageSize" },
            { "ProjectUrl", "projectUrl" },
            { "ReleaseNotes", "releaseNotes"},
            { "RequireLicenseAcceptance", "requireLicenseAcceptance"},
            { "Summary", "summary" },
            { "Title", "title" },
            { "LicenseUrl", "licenseUrl" },
            { "LicenseReportUrl", "licenseReportUrl" },
            { "MinClientVersion", "minClientVersion" },
            { "DownloadCount", "downloadCount" }
        };
        private static readonly IDictionary<string, string> ListFieldNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Tags", "tag" },
            { "LicenseNames", "licenseName" }
        };

        static JObject CreateContent(string resourceUri, GalleryExportPackage export, DateTime timeStamp, Guid commitId)
        {
            JObject obj = new JObject();

            obj.Add("catalog:galleryKey", export.Package["Key"].ToObject<int>());
            obj.Add("catalog:galleryChecksum", export.Package["DatabaseChecksum"].ToObject<string>());

            obj.Add("url", resourceUri);

            obj.Add("@type", "Package");

            obj.Add("catalog:commitId", commitId);

            obj.Add("catalog:commitTimestamp", timeStamp.ToString("O"));

            obj.Add("id", export.Id);

            foreach (JProperty property in export.Package.Properties().Where(p => p.Value != null && p.Value.Type != JTokenType.Null))
            {
                if (ListFieldNames.ContainsKey(property.Name))
                {
                    var name = ListFieldNames[property.Name];
                    char[] trimChar = { ',', ' ', '\t', '|', ';' };

                    IEnumerable<string> fields = property.Value.ToString()
                        .Split(trimChar)
                        .Select((w) => w.Trim(trimChar))
                        .Where((w) => w.Length > 0);

                    JArray valueArray = new JArray();
                    foreach (string field in fields)
                    {
                        valueArray.Add(field);
                    }
                    obj.Add(name, valueArray);
                }
                else
                {
                    string name;
                    if (FieldMappings.TryGetValue(property.Name, out name))
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
                        dependencyGroupDependency.Add("packageId", id);
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
