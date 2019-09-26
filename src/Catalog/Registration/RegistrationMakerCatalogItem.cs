// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using VDS.RDF;
using VDS.RDF.Query;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class RegistrationMakerCatalogItem : CatalogItem
    {
        public delegate IGraph PostProcessGraph(IGraph graph);

        private readonly Uri _catalogUri;
        private readonly IGraph _catalogItem;
        private Uri _itemAddress;
        private readonly Uri _packageContentBaseAddress;
        private readonly Uri _galleryBaseAddress;
        private Uri _packageContentAddress;
        private readonly Uri _registrationBaseAddress;
        private Uri _registrationAddress;
        private DateTime _publishedDate;
        private bool _listed;
        private readonly PostProcessGraph _postProcessGraph;
        private readonly bool _forcePathProviderForIcons;

        // This should be set before class is instantiated
        public static IPackagePathProvider PackagePathProvider = null;

        public RegistrationMakerCatalogItem(
            Uri catalogUri,
            IGraph catalogItem,
            Uri registrationBaseAddress,
            bool isExistingItem,
            PostProcessGraph postProcessGraph,
            bool forcePathProviderForIcons,
            Uri packageContentBaseAddress = null,
            Uri galleryBaseAddress = null)
        {
            _catalogUri = catalogUri;
            _catalogItem = catalogItem;
            _packageContentBaseAddress = packageContentBaseAddress;
            _galleryBaseAddress = galleryBaseAddress;
            _registrationBaseAddress = registrationBaseAddress;
            _postProcessGraph = postProcessGraph;
            _forcePathProviderForIcons = forcePathProviderForIcons;

            IsExistingItem = isExistingItem;
        }

        public override StorageContent CreateContent(CatalogContext context)
        {
            IGraph graph = new Graph();
            INode subject = graph.CreateUriNode(GetItemAddress());

            graph.CreateUriNode(_catalogUri);

            graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.Package));
            graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.Permalink));
            graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.CatalogEntry), graph.CreateUriNode(_catalogUri));
            graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.Registration), graph.CreateUriNode(GetRegistrationAddress()));

            graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.PackageContent), graph.CreateUriNode(GetPackageContentAddress()));
            graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.Published), graph.CreateLiteralNode(GetPublishedDate().ToString("O"), Schema.DataTypes.DateTime));
            graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.Listed), graph.CreateLiteralNode(_listed.ToString(), Schema.DataTypes.Boolean));

            JObject frame = context.GetJsonLdContext("context.Package.json", Schema.DataTypes.Package);
            return new JTokenStorageContent(Utils.CreateJson(graph, frame), "application/json", "no-store");
        }

        public bool IsExistingItem { get; private set; }

        public override Uri GetItemType()
        {
            return Schema.DataTypes.Package;
        }

        public override Uri GetItemAddress()
        {
            if (_itemAddress == null)
            {
                INode subject = _catalogItem.CreateUriNode(_catalogUri);
                string version = GetRequiredObject(_catalogItem, subject, Schema.Predicates.Version)
                    .ToLowerInvariant();

                version = NuGetVersionUtility.NormalizeVersion(version);

                _itemAddress = new Uri(BaseAddress, version + ".json");
            }

            return _itemAddress;
        }

        private Uri GetRegistrationAddress()
        {
            if (_registrationAddress == null)
            {
                INode subject = _catalogItem.CreateUriNode(_catalogUri);
                string id = GetRequiredObject(_catalogItem, subject, Schema.Predicates.Id).ToLowerInvariant();
                string path = string.Format("{0}/index.json", id.ToLowerInvariant());
                _registrationAddress = new Uri(_registrationBaseAddress, path);
            }

            return _registrationAddress;
        }

        private DateTime GetPublishedDate()
        {
            if (_publishedDate == default(DateTime))
            {
                INode subject = _catalogItem.CreateUriNode(_catalogUri);
                var pubTriple = _catalogItem.GetTriplesWithSubjectPredicate(subject, _catalogItem.CreateUriNode(Schema.Predicates.Published)).SingleOrDefault();

                if (pubTriple != null)
                {
                    if (pubTriple.Object is ILiteralNode node)
                    {
                        _publishedDate = DateTime.Parse(node.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                    }
                }
            }

            var utcYear = _publishedDate.ToUniversalTime().Year;
            if ((utcYear < 1900) ||
                (utcYear > 1900 && utcYear < 2010))
            {
                Trace.TraceWarning($"Package with published date less than 2010 encountered. Catalog URI: '{_catalogUri}'. Published date: '{_publishedDate:O}'");
            }

            _listed = utcYear != 1900;

            return _publishedDate;
        }

        private Uri GetPackageContentAddress()
        {
            if (PackagePathProvider == null)
            {
                throw new NullReferenceException("PackagePathProvider should not be null");
            }

            if (_packageContentAddress == null)
            {
                INode subject = _catalogItem.CreateUriNode(_catalogUri);

                Triple packageContentTriple = _catalogItem.GetTriplesWithSubjectPredicate(subject, _catalogItem.CreateUriNode(Schema.Predicates.PackageContent)).FirstOrDefault();
                if (packageContentTriple != null)
                {
                    _packageContentAddress = new Uri(packageContentTriple.Object.ToString());
                }
                else
                {
                    string id = GetRequiredObject(_catalogItem, subject, Schema.Predicates.Id).ToLowerInvariant();
                    string version = GetRequiredObject(_catalogItem, subject, Schema.Predicates.Version).ToLowerInvariant();
                    string path = PackagePathProvider.GetPackagePath(id, version);
                    _packageContentAddress = new Uri(_packageContentBaseAddress, path);
                }
            }

            return _packageContentAddress;
        }

        private string GetLicenseUrl()
        {
            INode subject = _catalogItem.CreateUriNode(_catalogUri);

            string packageId = GetRequiredObject(_catalogItem, subject, Schema.Predicates.Id);
            string packageVersion = NuGetVersionUtility.NormalizeVersion(GetRequiredObject(_catalogItem, subject, Schema.Predicates.Version));
            string licenseExpression = GetOptionalObject(_catalogItem, subject, Schema.Predicates.LicenseExpression);
            string licenseFile = GetOptionalObject(_catalogItem, subject, Schema.Predicates.LicenseFile);
            string licenseUrl = GetOptionalObject(_catalogItem, subject, Schema.Predicates.LicenseUrl);

            if (_galleryBaseAddress != null &&
                !string.IsNullOrWhiteSpace(packageId) &&
                !string.IsNullOrWhiteSpace(packageVersion) &&
                (!string.IsNullOrWhiteSpace(licenseExpression) ||
                 !string.IsNullOrWhiteSpace(licenseFile)))
            {
                return LicenseHelper.GetGalleryLicenseUrl(packageId, packageVersion, _galleryBaseAddress);
            }

            if (!string.IsNullOrWhiteSpace(licenseUrl))
            {
                return licenseUrl;
            }

            return string.Empty;
        }

        public override IGraph CreatePageContent(CatalogContext context)
        {
            try
            {
                IGraph content;

                using (TripleStore store = new TripleStore())
                {
                    store.Add(_catalogItem, true);

                    SparqlParameterizedString sparql = new SparqlParameterizedString
                    {
                        CommandText = Utils.GetResource("sparql.ConstructRegistrationPageContentGraph.rq")
                    };

                    sparql.SetUri("package", GetItemAddress());
                    sparql.SetUri("catalogEntry", _catalogUri);
                    sparql.SetUri("baseAddress", BaseAddress);
                    sparql.SetUri("packageContent", GetPackageContentAddress());
                    sparql.SetUri("registrationBaseAddress", _registrationBaseAddress);
                    sparql.SetLiteral("licenseUrl", GetLicenseUrl());
                    sparql.SetLiteral("iconUrl", GetIconUrl());

                    content = SparqlHelpers.Construct(store, sparql.ToString());
                }

                return _postProcessGraph(content);
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("Exception processing catalog item {0}", _catalogUri), e);
            }
        }

        private string GetIconUrl()
        {
            var subject = _catalogItem.CreateUriNode(_catalogUri);

            var packageId = GetRequiredObject(_catalogItem, subject, Schema.Predicates.Id);
            var packageVersion = NuGetVersionUtility.NormalizeVersion(GetRequiredObject(_catalogItem, subject, Schema.Predicates.Version));
            var iconUrl = GetOptionalObject(_catalogItem, subject, Schema.Predicates.IconUrl);
            var iconFile = GetOptionalObject(_catalogItem, subject, Schema.Predicates.IconFile);

            var shouldUsePathProvider = !string.IsNullOrWhiteSpace(iconFile) || (_forcePathProviderForIcons && !string.IsNullOrWhiteSpace(iconUrl));

            if (shouldUsePathProvider && !string.IsNullOrWhiteSpace(packageId) && !string.IsNullOrWhiteSpace(packageVersion))
            {
                // The embedded icon file case. We assume here that catalog2dnx did its job
                // and extracted the icon file to the appropriate location.
                string path = PackagePathProvider.GetIconPath(packageId, packageVersion);
                return new Uri(_packageContentBaseAddress, path).AbsoluteUri;
            }

            return _forcePathProviderForIcons || iconUrl == null ? string.Empty : iconUrl;
        }

        private static string GetRequiredObject(IGraph graph, INode subject, Uri predicate)
        {
            var predicateNode = graph.CreateUriNode(predicate);
            var triple = graph.GetTriplesWithSubjectPredicate(subject, predicateNode).First();

            return triple.Object.ToString();
        }

        private static string GetOptionalObject(IGraph graph, INode subject, Uri predicate)
        {
            var predicateNode = graph.CreateUriNode(predicate);
            var triple = graph.GetTriplesWithSubjectPredicate(subject, predicateNode).FirstOrDefault();

            return triple?.Object.ToString();
        }
    }
}
