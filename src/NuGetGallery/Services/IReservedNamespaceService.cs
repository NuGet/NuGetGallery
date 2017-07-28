// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IReservedNamespaceService
    {
        Task AddReservedNamespaceAsync(ReservedNamespace prefix);

        Task DeleteReservedNamespaceAsync(ReservedNamespace prefix);

        ReservedNamespace FindReservedNamespaceForPrefix(string prefix);

        IList<ReservedNamespace> FindAllReservedNamespacesForPrefix(string prefix);

        Task AddOwnerToReservedNamespaceAsync(ReservedNamespace prefix, User user);

        Task DeleteOwnerFromReservedNamespaceAsync(ReservedNamespace prefix, User user);

        IList<User> GetAllOwnersForNamespace(ReservedNamespace prefix);

        IList<ReservedNamespace> GetAllReservedNamespacesForUser(User user);
    }
}