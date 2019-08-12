// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Documents;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface ILuceneDocumentFactory
    {
        Document Create(Package package);
    }
}