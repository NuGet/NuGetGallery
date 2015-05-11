// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace NuGet.Services.Metadata.Catalog.Pipeline
{
    public class PackagePipelineContext
    {
        public IDictionary<string, object> Shelf { get; private set; }

        public Uri Uri { get; set; }
        public Uri BaseAddress { get; private set; }

        public IList<PackageMetadataBase> StageResults { get; private set; }
        public IList<PackageMetadataBase> StagePageResults { get; private set; }

        public PackageMetadataBase Result { get; private set; }
        public PackageMetadataBase PageResult { get; private set; }

        public PackagePipelineContext(Uri baseAddress)
        {
            BaseAddress = baseAddress;
            Shelf = new Dictionary<string, object>();
            StageResults = new List<PackageMetadataBase>();
            StagePageResults = new List<PackageMetadataBase>();
            Result = null;
            Uri = new Uri("http://tempuri.org/package/");
        }

        public void MergeStageResults()
        {
            Result = new GraphPackageMetadata();
            foreach (PackageMetadataBase packageMetadata in StageResults)
            {
                Result.Merge(packageMetadata);
            }

            PageResult = new GraphPackageMetadata();
            foreach (PackageMetadataBase packageMetadata in StagePageResults)
            {
                PageResult.Merge(packageMetadata);
            }
        }
    }
}
