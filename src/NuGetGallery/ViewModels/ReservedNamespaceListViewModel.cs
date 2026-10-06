// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class ReservedNamespaceListViewModel
    {
        public IEnumerable<ReservedNamespaceListItemViewModel> ReservedNamespaces { get; }

        public ReservedNamespaceListViewModel(
            ICollection<ReservedNamespace> reservedNamespacesList,
            IEnumerable<NamespaceReservationRequest> requests = null,
            IEnumerable<User> knownOwners = null)
        {
            var reservedRows = reservedNamespacesList
                .Select(rn => new ReservedNamespaceListItemViewModel(rn));
            var owners = knownOwners?.ToList();
            var requestRows = (requests ?? Enumerable.Empty<NamespaceReservationRequest>())
                .Select(request => new ReservedNamespaceListItemViewModel(request, owners))
                .Where(row => !row.IsFulfilledBy(reservedNamespacesList));

            // Keep every confirmed reservation, including those with no request record,
            // first in the same table as the customer's outstanding requests.
            ReservedNamespaces = reservedRows.Concat(requestRows).ToArray();
        }
    }
}