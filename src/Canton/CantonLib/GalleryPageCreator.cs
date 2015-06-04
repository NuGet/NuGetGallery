// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Canton
{
    public class GalleryPageCreator : PageCreator
    {
        private Action<Uri> _itemComplete;

        public GalleryPageCreator(Storage storage, Action<Uri> itemComplete)
            : base(storage)
        {
            _itemComplete = itemComplete;
            _threads = 64;
        }

        protected override Uri CreateCatalogPage(CatalogItem item)
        {
            // save the content to a non-temp location
            StorageContent content = item.CreateContent(Context);

            if (content != null)
            {
                var resourceUri = item.GetItemAddress();
                Storage.Save(resourceUri, content, CancellationToken.None).Wait();
                return resourceUri;
            }

            return null;
        }

        //protected override Uri CreateIndexEntry(CatalogItem item, Uri resourceUri, Guid commitId, DateTime commitTimeStamp)
        //{
        //    // we don't want this for gallery pages
        //    return null;
        //}

        protected override void CommitItemComplete(Uri resourceUri)
        {
            _itemComplete(resourceUri);
        }
    }
}
