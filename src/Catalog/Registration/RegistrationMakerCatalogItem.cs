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

        // This should be set before class is instantiated
        public static IPackagePathProvider PackagePathProvider = null;

        public RegistrationMakerCatalogItem(Uri catalogUri, IGraph catalogItem, Uri registrationBaseAddress, bool isExistingItem, Uri packageContentBaseAddress = null, Uri galleryBaseAddress = null)
        {
            _catalogUri = catalogUri;
            _catalogItem = catalogItem;
            _packageContentBaseAddress = packageContentBaseAddress;
            _galleryBaseAddress = galleryBaseAddress;
            _registrationBaseAddress = registrationBaseAddress;

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
                string version = _catalogItem.GetTriplesWithSubjectPredicate(subject, _catalogItem.CreateUriNode(Schema.Predicates.Version))
                    .FirstOrDefault()
                    .Object
                    .ToString()
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
                string id = _catalogItem.GetTriplesWithSubjectPredicate(subject, _catalogItem.CreateUriNode(Schema.Predicates.Id)).FirstOrDefault().Object.ToString().ToLowerInvariant();
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
                    ILiteralNode node = pubTriple.Object as ILiteralNode;

                    if (node != null)
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
                    string id = _catalogItem.GetTriplesWithSubjectPredicate(subject, _catalogItem.CreateUriNode(Schema.Predicates.Id)).FirstOrDefault().Object.ToString().ToLowerInvariant();
                    string version = _catalogItem.GetTriplesWithSubjectPredicate(subject, _catalogItem.CreateUriNode(Schema.Predicates.Version)).FirstOrDefault().Object.ToString().ToLowerInvariant();
                    string path = PackagePathProvider.GetPackagePath(id, version);
                    _packageContentAddress = new Uri(_packageContentBaseAddress, path);
                }
            }

            return _packageContentAddress;
        }

        private string GetLicenseUrl()
        {
            INode subject = _catalogItem.CreateUriNode(_catalogUri);

            string packageId = _catalogItem.GetTriplesWithSubjectPredicate(subject, _catalogItem.CreateUriNode(Schema.Predicates.Id)).FirstOrDefault().Object.ToString();
            string packageVersion = NuGetVersionUtility.NormalizeVersion(_catalogItem.GetTriplesWithSubjectPredicate(subject, _catalogItem.CreateUriNode(Schema.Predicates.Version)).FirstOrDefault().Object.ToString());
            Triple licenseExpression = _catalogItem.GetTriplesWithSubjectPredicate(subject, _catalogItem.CreateUriNode(Schema.Predicates.LicenseExpression)).FirstOrDefault();
            Triple licenseFile = _catalogItem.GetTriplesWithSubjectPredicate(subject, _catalogItem.CreateUriNode(Schema.Predicates.LicenseFile)).FirstOrDefault();
            Triple licenseUrl = _catalogItem.GetTriplesWithSubjectPredicate(subject, _catalogItem.CreateUriNode(Schema.Predicates.LicenseUrl)).FirstOrDefault();

            if (_galleryBaseAddress != null &&
                !string.IsNullOrWhiteSpace(packageId) &&
                !string.IsNullOrWhiteSpace(packageVersion) &&
                (!string.IsNullOrWhiteSpace(licenseExpression?.Object.ToString()) ||
                 !string.IsNullOrWhiteSpace(licenseFile?.Object.ToString())))
            {
                return LicenseHelper.GetGalleryLicenseUrl(packageId, packageVersion, _galleryBaseAddress);
            }

            if (licenseUrl != null)
            {
                return licenseUrl.Object.ToString();
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

                    SparqlParameterizedString sparql = new SparqlParameterizedString();
                    sparql.CommandText = Utils.GetResource("sparql.ConstructRegistrationPageContentGraph.rq");

                    sparql.SetUri("package", GetItemAddress());
                    sparql.SetUri("catalogEntry", _catalogUri);
                    sparql.SetUri("baseAddress", BaseAddress);
                    sparql.SetUri("packageContent", GetPackageContentAddress());
                    sparql.SetUri("registrationBaseAddress", _registrationBaseAddress);
                    sparql.SetLiteral("licenseUrl", GetLicenseUrl());

                    content = SparqlHelpers.Construct(store, sparql.ToString());
                }

                return content;
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("Exception processing catalog item {0}", _catalogUri), e);
            }
        }
    }
}
