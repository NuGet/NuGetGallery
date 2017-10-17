// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public class ReservedNamespaceViewModel
    {
        public IEnumerable<ReservedNamespaceListItemViewModel> ReservedNamespaces { get; }

        public ReservedNamespaceViewModel(ICollection<ReservedNamespace> reservedNamespacesList)
        {
            ReservedNamespaces = reservedNamespacesList
                .ToList()
                .Select(rn => new ReservedNamespaceListItemViewModel(rn));
        }
    }
}