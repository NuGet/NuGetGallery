// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Writing;

namespace NuGet.Canton
{
    public class PageCreator : AppendOnlyCatalogWriter
    {
        protected int _threads = 8;
        private IEnumerable<GraphAddon> _addons;

        public PageCreator(Storage storage)
            : this(storage, Enumerable.Empty<GraphAddon>())
        {

        }

        public PageCreator(Storage storage, IEnumerable<GraphAddon> addons)
            : base(storage)
        {
            _addons = addons;
        }

        public override async Task Commit(DateTime commitTimeStamp, IGraph commitMetadata = null)
        {
            if (!_open)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            if (_batch.Count == 0)
            {
                return;
            }

            //  the commitId is only used for tracing and trouble shooting
            Guid commitId = Guid.NewGuid();

            //  save items
            IDictionary<string, CatalogItemSummary> newItemEntries = await SaveItems(commitId, commitTimeStamp);

            _batch.Clear();
        }

        async Task<IDictionary<string, CatalogItemSummary>> SaveItems(Guid commitId, DateTime commitTimeStamp)
        {
            ConcurrentDictionary<string, CatalogItemSummary> pageItems = new ConcurrentDictionary<string, CatalogItemSummary>();

            ParallelOptions options = new ParallelOptions();
            options.MaxDegreeOfParallelism = _threads;

            var items = _batch.ToArray();

            ConcurrentDictionary<CatalogItem, Uri> tmpPages = new ConcurrentDictionary<CatalogItem, Uri>();

            Parallel.ForEach(items, options, item =>
            {
                Uri resourceUri = null;

                try
                {
                    item.TimeStamp = commitTimeStamp;
                    item.CommitId = commitId;
                    item.BaseAddress = Storage.BaseAddress;

                    Uri catalogPageUri = CreateCatalogPage(item);

                    //CommitItemComplete(catalogPageUri);

                    resourceUri = item.GetItemAddress();

                    if (!tmpPages.TryAdd(item, catalogPageUri))
                    {
                        throw new Exception("duplicate item");
                    }


                    //if (catalogPageUri != null)
                    //{
                    //    Uri indexPageUri = CreateIndexEntry(item, catalogPageUri, commitId, commitTimeStamp);

                    //    CommitItemComplete(catalogPageUri, indexPageUri);
                    //}
                    //else
                    //{
                    //    Debug.Fail("Missing catalog content");
                    //}
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("item uri: {0}", resourceUri == null ? "none" : resourceUri.AbsoluteUri), e);
                }
            });

            // make sure to commit these in the correct order
            foreach (var item in items)
            {
                Uri pageUri = null;
                tmpPages.TryGetValue(item, out pageUri);
                CommitItemComplete(pageUri);
            }

            return pageItems;
        }

        protected virtual Uri CreateCatalogPage(CatalogItem item)
        {
            Uri tmpUri = GetTempUri("catalogpage", "ttl");

            using (IGraph graph = item.CreateContentGraph(Context))
            {
                if (_addons != null)
                {
                    INode rdfTypePredicate = graph.CreateUriNode(Schema.Predicates.Type);
                    Triple resource = graph.GetTriplesWithPredicateObject(rdfTypePredicate, graph.CreateUriNode(item.GetItemType())).First();

                    foreach (var addon in _addons)
                    {
                        addon.ApplyToGraph(graph, (IUriNode)resource.Subject);
                    }
                }

                SaveGraph(graph, tmpUri).Wait();
            }

            return tmpUri;
        }

        //protected virtual Uri CreateIndexEntry(CatalogItem item, Uri resourceUri, Guid commitId, DateTime commitTimeStamp)
        //{
        //    Uri tmpUri = GetTempUri("catalogindexpage", "ttl");

        //    using (IGraph pageContent = item.CreatePageContent(Context))
        //    {
        //        AddCatalogEntryData(pageContent, item.GetItemType(), resourceUri, commitId, commitTimeStamp);

        //        SaveGraph(pageContent, tmpUri).Wait();
        //    }

        //    return tmpUri;
        //}

        private async Task SaveGraph(IGraph graph, Uri uri)
        {
            StringBuilder sb = new StringBuilder();
            using (var stringWriter = new System.IO.StringWriter(sb))
            {
                CompressingTurtleWriter turtleWriter = new CompressingTurtleWriter();
                turtleWriter.Save(graph, stringWriter);
            }

            StorageContent content = new StringStorageContent(sb.ToString(), "application/json", "no-store");

            await Storage.Save(uri, content);
        }

        protected Uri GetTempUri(string folder, string extension)
        {
            return new Uri(String.Format(CultureInfo.InvariantCulture, "{0}{1}/{2}.{3}", Storage.BaseAddress.AbsoluteUri, folder, Guid.NewGuid().ToString(), extension).ToLowerInvariant());
        }

        protected virtual void CommitItemComplete(Uri resourceUri)
        {
            // this should be overridden
        }

        //private void AddCatalogEntryData(IGraph pageContent, Uri itemType, Uri resourceUri, Guid commitId, DateTime commitTimeStamp)
        //{
            
        //    var pageContentRoot = pageContent.CreateUriNode(resourceUri);
        //    pageContent.Assert(pageContentRoot, pageContent.CreateUriNode(Schema.Predicates.Type), pageContent.CreateUriNode(itemType));
        //    pageContent.Assert(pageContentRoot, pageContent.CreateUriNode(Schema.Predicates.CatalogCommitId), pageContent.CreateLiteralNode(commitId.ToString()));
        //    pageContent.Assert(pageContentRoot,
        //        pageContent.CreateUriNode(Schema.Predicates.CatalogCommitId),
        //        pageContent.CreateLiteralNode(commitTimeStamp.ToString("O"), Schema.DataTypes.DateTime));
        //}

        async Task SaveRoot(Guid commitId, DateTime commitTimeStamp, IDictionary<string, CatalogItemSummary> pageEntries, IGraph commitMetadata)
        {
            await SaveIndexResource(RootUri, Schema.DataTypes.CatalogRoot, commitId, commitTimeStamp, pageEntries, null, commitMetadata, GetAdditionalRootType());
        }
    }
}
