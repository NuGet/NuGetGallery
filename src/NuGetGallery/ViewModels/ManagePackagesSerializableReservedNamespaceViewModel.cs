// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public class ManagePackagesSerializableReservedNamespaceViewModel
    {
        public ManagePackagesSerializableReservedNamespaceViewModel(
            ReservedNamespaceListItemViewModel reservedNamespace,
            RouteUrlTemplate<string> searchUrlTemplate,
            RouteUrlTemplate<User> profileUrlTemplate)
        {
            Pattern = reservedNamespace.GetPattern();
            SearchUrl = searchUrlTemplate.Resolve(reservedNamespace.Value);
            Owners = reservedNamespace.Owners.Select(o => new ManagePackagesSerializableOwnerViewModel(o, profileUrlTemplate));
            IsPublic = reservedNamespace.IsPublic;
        }

        public string Pattern { get; }
        public string SearchUrl { get; }
        public IEnumerable<ManagePackagesSerializableOwnerViewModel> Owners { get; }
        public bool IsPublic { get; }
    }
}