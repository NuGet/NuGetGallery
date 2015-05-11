// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public interface ICatalogGraphPersistence
    {
        Task SaveGraph(Uri resourceUri, IGraph graph, Uri typeUri);
        Task<IGraph> LoadGraph(Uri resourceUri);
        Uri CreatePageUri(Uri baseAddress, string relativeAddress);
    }
}
