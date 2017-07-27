// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IReservedNamespaceService
    {
        Task AddReservedNamespaceAsync(ReservedNamespace prefix);

        ReservedNamespace FindReservedNamespacesForPrefix(string prefix);

        Task DeleteReservedNamespaceAsync(ReservedNamespace prefix);

        Task AddUserToReservedNamespaceAsync(ReservedNamespace prefix, User user);

        Task DeleteUserFromReservedNamespaceAsync(ReservedNamespace prefix, User user);

        IList<User> GetAllUsersForNamespace(ReservedNamespace prefix);

        IList<ReservedNamespace> GetAllReservedNamespacesForUser(User user);
    }
}