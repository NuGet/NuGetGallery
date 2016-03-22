// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Linq;
using VDS.RDF;
using VDS.RDF.Query;
using System.Globalization;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class RegistrationMakerCatalogItem : CatalogItem
    {
        private readonly Uri _catalogUri;
        private readonly IGraph _catalogItem;
        private Uri _itemAddress;
        private readonly Uri _packageContentBaseAddress;
        private Uri _packageContentAddress;
        private readonly Uri _registrationBaseAddress;
        private Uri _registrationAddress;
        private DateTime _publishedDate;
        private bool _listed;
        private readonly bool _isExistingItem;

        public RegistrationMakerCatalogItem(Uri catalogUri, IGraph catalogItem, Uri registrationBaseAddress, bool isExistingItem, Uri packageContentBaseAddress = null)
        {
            _catalogUri = catalogUri;
            _catalogItem = catalogItem;
            _packageContentBaseAddress = packageContentBaseAddress;
            _registrationBaseAddress = registrationBaseAddress;
            _isExistingItem = isExistingItem;
        }

        public override StorageContent CreateContent(CatalogContext context)
        {
            if (_isExistingItem)
            {
                return null;
            }

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

        public override Uri GetItemType()
        {
            return Schema.DataTypes.Package;
        }

        public override Uri GetItemAddress()
        {
            if (_itemAddress == null)
            {
                INode subject = _catalogItem.CreateUriNode(_catalogUri);
                string version = _catalogItem.GetTriplesWithSubjectPredicate(subject, _catalogItem.CreateUriNode(Schema.Predicates.Version)).FirstOrDefault().Object.ToString().ToLowerInvariant();
                _itemAddress = new Uri(BaseAddress, version + ".json");
            }

            return _itemAddress;
        }

        Uri GetRegistrationAddress()
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
                
        DateTime GetPublishedDate()
        {
            if (_publishedDate == DateTime.MinValue)
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

            _listed = (_publishedDate.Year == 1900) ? false : true;

            return _publishedDate;

        }

        Uri GetPackageContentAddress()
        {
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
                    string path = string.Format("packages/{0}.{1}.nupkg", id.ToLowerInvariant(), version.ToLowerInvariant());
                    _packageContentAddress = new Uri(_packageContentBaseAddress, path);
                }
            }

            return _packageContentAddress;
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
