// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    public class CatalogPageCreator : PageCreator
    {
        private Action<Uri> _itemComplete;

        public CatalogPageCreator(Storage storage, Action<Uri> itemComplete)
            : this (storage, itemComplete, Enumerable.Empty<GraphAddon>())
        {

        }

        public CatalogPageCreator(Storage storage, Action<Uri> itemComplete, IEnumerable<GraphAddon> addons)
            : base(storage, addons)
        {
            _itemComplete = itemComplete;
            _threads = 8;
        }

        protected override void CommitItemComplete(Uri resourceUri)
        {
            _itemComplete(resourceUri);
        }
    }
}
