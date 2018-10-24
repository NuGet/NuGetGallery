// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class ReservedNamespaceListViewModel
    {
        public IEnumerable<ReservedNamespaceListItemViewModel> ReservedNamespaces { get; }

        public ReservedNamespaceListViewModel(ICollection<ReservedNamespace> reservedNamespacesList)
        {
            ReservedNamespaces = reservedNamespacesList
                .Select(rn => new ReservedNamespaceListItemViewModel(rn));
        }
    }
}