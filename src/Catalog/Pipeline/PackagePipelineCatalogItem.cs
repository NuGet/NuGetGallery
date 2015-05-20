// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.IO;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Pipeline
{
    public class PackagePipelineCatalogItem : AppendOnlyCatalogItem
    {
        PackagePipeline _pipeline;
        PipelinePackage _pipelinePackage;
        Uri _itemType;
        Uri _itemAddress;
        IGraph _pageContent;

        public PackagePipelineCatalogItem(PackagePipeline pipeline, Uri itemType, Stream stream, DateTime published, string owner)
        {
            _pipeline = pipeline;
            _itemType = itemType;
            _pipelinePackage = new PipelinePackage(stream, published, owner);
        }

        public override Uri GetItemType()
        {
            return _itemType;
        }

        public override StorageContent CreateContent(CatalogContext context)
        {
            PackagePipelineContext pipelineContext = new PackagePipelineContext(GetBaseAddress());

            _pipeline.Execute(_pipelinePackage, pipelineContext);

            _pageContent = ((GraphPackageMetadata)pipelineContext.PageResult).Graph;

            _itemAddress = pipelineContext.Uri;

            JObject frame = context.GetJsonLdContext("context.PackageDetails.json", Schema.DataTypes.PackageDetails);
            string json = pipelineContext.Result.ToContent(frame).ToString();

            return new StringStorageContent(json, "application/json", "no-store");
        }

        public override Uri GetItemAddress()
        {
            return _itemAddress;
        }

        public override IGraph CreatePageContent(CatalogContext context)
        {
            return _pageContent;
        }
    }
}
