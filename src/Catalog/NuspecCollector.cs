// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using VDS.RDF;
using VDS.RDF.Query;

namespace NuGet.Services.Metadata.Catalog
{
    public class NuspecCollector : StoreCollector
    {
        static readonly XNamespace nuget = XNamespace.Get("http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd");

        Storage _storage;

        public NuspecCollector(Uri index, Storage storage, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, new Uri[] { Schema.DataTypes.Package }, handlerFunc)
        {
            _storage = storage;
        }

        protected override async Task ProcessStore(TripleStore store, CancellationToken cancellationToken)
        {
            try
            {
                SparqlResultSet packages = SparqlHelpers.Select(store, Utils.GetResource("sparql.SelectPackage.rq"));

                IList<Uri> packageUris = new List<Uri>();

                foreach (SparqlResult row in packages)
                {
                    Uri packageUri = ((IUriNode)row["package"]).Uri;
                    packageUris.Add(packageUri);
                }

                IList<XDocument> nuspecs = new List<XDocument>(); 

                foreach (Uri packageUri in packageUris)
                {
                    XDocument nuspec = new XDocument();

                    XElement metadata = CreateNuspecMetadata(store, packageUri);

                    XElement tags = CreateNuspecMetadataTags(store, packageUri);
                    if (tags != null)
                    {
                        metadata.Add(tags);
                    }

                    XElement dependencies = CreateNuspecMetadataDependencies(store, packageUri);
                    if (dependencies != null)
                    {
                        metadata.Add(dependencies);
                    }

                    //TODO: references, reference groups etc.

                    XElement frameworkAssemblies = CreateNuspecMetadataFrameworkAssembly(store, packageUri);
                    if (frameworkAssemblies != null)
                    {
                        metadata.Add(frameworkAssemblies);
                    }

                    nuspec.Add(new XElement(nuget.GetName("package"), metadata));

                    nuspecs.Add(nuspec);
                }

                await SaveAllNuspecs(nuspecs, cancellationToken);
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
            }
        }

        static XElement CreateNuspecMetadata(TripleStore store, Uri packageUri)
        {
            SparqlParameterizedString sparql = new SparqlParameterizedString();
            sparql.CommandText = Utils.GetResource("sparql.SelectPackageDetails.rq");
            sparql.SetUri("package", packageUri);
            SparqlResult packageInfo = SparqlHelpers.Select(store, sparql.ToString()).FirstOrDefault();

            if (packageInfo == null)
            {
                throw new ArgumentException(string.Format("no package details for {0}", packageUri));
            }

            XElement metadata = new XElement(nuget.GetName("metadata"));

            metadata.Add(new XElement(nuget.GetName("id"), packageInfo["id"].ToString()));
            metadata.Add(new XElement(nuget.GetName("version"), packageInfo["version"].ToString()));

            //  in the nuspec the published and owners fields are not present and ignored respectfully

            //if (packageInfo.HasBoundValue("published"))
            //{
            //    DateTime val = DateTime.Parse(((ILiteralNode)packageInfo["published"]).Value);
            //    metadata.Add(new XElement(nuget.GetName("published"), val.ToUniversalTime().ToString("O")));
            //}

            if (packageInfo.HasBoundValue("authors"))
            {
                metadata.Add(new XElement(nuget.GetName("authors"), packageInfo["authors"].ToString()));
            }

            if (packageInfo.HasBoundValue("description"))
            {
                metadata.Add(new XElement(nuget.GetName("description"), packageInfo["description"].ToString()));
            }

            if (packageInfo.HasBoundValue("summary"))
            {
                metadata.Add(new XElement(nuget.GetName("summary"), packageInfo["summary"].ToString()));
            }

            if (packageInfo.HasBoundValue("language"))
            {
                metadata.Add(new XElement(nuget.GetName("language"), packageInfo["language"].ToString()));
            }

            if (packageInfo.HasBoundValue("title"))
            {
                metadata.Add(new XElement(nuget.GetName("title"), packageInfo["title"].ToString()));
            }

            if (packageInfo.HasBoundValue("targetFramework"))
            {
                metadata.Add(new XElement(nuget.GetName("targetFramework"), packageInfo["targetFramework"].ToString()));
            }

            if (packageInfo.HasBoundValue("requireLicenseAcceptance"))
            {
                bool val = bool.Parse(((ILiteralNode)packageInfo["requireLicenseAcceptance"]).Value);
                metadata.Add(new XElement(nuget.GetName("requireLicenseAcceptance"), val));
            }

            if (packageInfo.HasBoundValue("licenseUrl"))
            {
                metadata.Add(new XElement(nuget.GetName("licenseUrl"), packageInfo["licenseUrl"].ToString()));
            }

            if (packageInfo.HasBoundValue("iconUrl"))
            {
                metadata.Add(new XElement(nuget.GetName("iconUrl"), packageInfo["iconUrl"].ToString()));
            }

            if (packageInfo.HasBoundValue("projectUrl"))
            {
                metadata.Add(new XElement(nuget.GetName("projectUrl"), packageInfo["projectUrl"].ToString()));
            }

            if (packageInfo.HasBoundValue("releaseNotes"))
            {
                metadata.Add(new XElement(nuget.GetName("releaseNotes"), packageInfo["releaseNotes"].ToString()));
            }

            if (packageInfo.HasBoundValue("copyright"))
            {
                metadata.Add(new XElement(nuget.GetName("copyright"), packageInfo["copyright"].ToString()));
            }

            if (packageInfo.HasBoundValue("minClientVersion"))
            {
                metadata.Add(new XAttribute("minClientVersion", packageInfo["minClientVersion"].ToString()));
            }

            if (packageInfo.HasBoundValue("developmentDependency"))
            {
                bool val = bool.Parse(((ILiteralNode)packageInfo["developmentDependency"]).Value);
                metadata.Add(new XElement(nuget.GetName("developmentDependency"), val));
            }

            return metadata;
        }

        static XElement CreateNuspecMetadataTags(TripleStore store, Uri packageUri)
        {
            SparqlParameterizedString sparql = new SparqlParameterizedString();
            sparql.CommandText = Utils.GetResource("sparql.SelectPackageTags.rq");
            sparql.SetUri("package", packageUri);
            SparqlResultSet packageTags = SparqlHelpers.Select(store, sparql.ToString());

            StringBuilder sb = new StringBuilder();

            foreach (SparqlResult row in packageTags)
            {
                sb.Append(row["tag"].ToString());
                sb.Append(' ');
            }

            string tags = sb.ToString().Trim(' ');

            if (tags.Length > 0)
            {
                return new XElement(nuget.GetName("tags"), tags);
            }

            return null;
        }

        static XElement CreateNuspecMetadataDependencies(TripleStore store, Uri packageUri)
        {
            SparqlParameterizedString sparql = new SparqlParameterizedString();
            sparql.CommandText = Utils.GetResource("sparql.SelectPackageDependencies.rq");
            sparql.SetUri("package", packageUri);
            SparqlResultSet packageDependencies = SparqlHelpers.Select(store, sparql.ToString());

            if (packageDependencies.Count > 0)
            {
                XElement dependencies = new XElement(nuget.GetName("dependencies"));

                IDictionary<string, IDictionary<string, string>> groups = new Dictionary<string, IDictionary<string, string>>();

                foreach (SparqlResult row in packageDependencies)
                {
                    string targetFramework = row["targetFramework"].ToString();

                    IDictionary<string, string> group;
                    if (!groups.TryGetValue(targetFramework, out group))
                    {
                        group = new Dictionary<string, string>();
                        groups.Add(targetFramework, group);
                    }

                    string id = row["id"].ToString();

                    string range = row.HasBoundValue("range") ? row["range"].ToString() : null;

                    group.Add(id, range);
                }

                if (groups.Count == 1 && groups.First().Key == "")
                {
                    AddDependencies(dependencies, groups.First().Value);
                }
                else
                {
                    foreach (KeyValuePair<string, IDictionary<string, string>> groupsItem in groups)
                    {
                        XElement group = new XElement(nuget.GetName("group"));

                        if (groupsItem.Key != "")
                        {
                            group.Add(new XAttribute("targetFramework", groupsItem.Key));
                        }

                        AddDependencies(group, groupsItem.Value);

                        dependencies.Add(group);
                    }
                }

                return dependencies;
            }

            return null;
        }

        static void AddDependencies(XElement parent, IDictionary<string, string> values)
        {
            foreach (KeyValuePair<string, string> dependencyItem in values)
            {
                XElement dependency = new XElement(nuget.GetName("dependency"));

                dependency.Add(new XAttribute("id", dependencyItem.Key));

                if (dependencyItem.Value != null)
                {
                    dependency.Add(new XAttribute("version", dependencyItem.Value));
                }

                parent.Add(dependency);
            }
        }

        static XElement CreateNuspecMetadataFrameworkAssembly(TripleStore store, Uri packageUri)
        {
            SparqlParameterizedString sparql = new SparqlParameterizedString();
            sparql.CommandText = Utils.GetResource("sparql.SelectPackageFrameworkAssembly.rq");
            sparql.SetUri("package", packageUri);
            SparqlResultSet packageFrameworkAssembly = SparqlHelpers.Select(store, sparql.ToString());

            if (packageFrameworkAssembly.Count > 0)
            {
                XElement frameworkAssemblies = new XElement(nuget.GetName("frameworkAssemblies"));

                foreach (SparqlResult row in packageFrameworkAssembly)
                {
                    string targetFramework = row["targetFramework"].ToString();
                    string assembly = row["assembly"].ToString();

                    XElement frameworkAssembly = new XElement(nuget.GetName("frameworkAssembly"));

                    frameworkAssembly.Add(new XAttribute("assemblyName", assembly));
                    frameworkAssembly.Add(new XAttribute("targetFramework", targetFramework));

                    frameworkAssemblies.Add(frameworkAssembly);
                }

                return frameworkAssemblies;
            }

            return null;
        }

        async Task SaveAllNuspecs(IList<XDocument> nuspecs, CancellationToken cancellationToken)
        {
            IList<Task> tasks = new List<Task>();

            foreach (XDocument nuspec in nuspecs)
            {
                tasks.Add(SaveNuspec(nuspec, cancellationToken));
            }

            await Task.WhenAll(tasks.ToArray());
        }

        Task SaveNuspec(XDocument nuspec, CancellationToken cancellationToken)
        {
            string relativeAddress = Utils.GetNuspecRelativeAddress(nuspec);

            Uri resourceUri = new Uri(_storage.BaseAddress, relativeAddress);

            StorageContent content = new StringStorageContent(
                nuspec.ToString(),
                contentType: "text/xml",
                cacheControl: "public, max-age=300, s-maxage=300");

            return _storage.Save(resourceUri, content, cancellationToken);
        }
    }
}
