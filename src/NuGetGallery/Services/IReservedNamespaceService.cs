// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IReservedNamespaceService
    {
        /// <summary>
        /// Create a new namespace with the given prefix
        /// </summary>
        /// <param name="prefix">The reserved namespace to be created</param>
        /// <returns>Awaitable Task</returns>
        Task AddReservedNamespaceAsync(ReservedNamespace prefix);

        /// <summary>
        /// Deallocate the reserved namespace with the given prefix, also removes
        /// the verified property on all the package registrations which match
        /// this reserved namespace only.
        /// </summary>
        /// <param name="prefix">The reserved namespace to be deleted</param>
        /// <returns>Awaitable Task</returns>
        Task DeleteReservedNamespaceAsync(string prefix);

        /// <summary>
        /// Adds the specified user as an owner to the reserved namespace.
        /// Also, all the package registrations owned by this user which match the 
        /// specified namespace will be marked as verified.
        /// </summary>
        /// <param name="prefix">The reserved namespace to modify</param>
        /// <param name="username">The user who gets ownership of the namespace</param>
        /// <returns>Awaitable Task</returns>
        Task AddOwnerToReservedNamespaceAsync(string prefix, string username);

        /// <summary>
        /// Remove the specified user as an owner from the reserved namespace.
        /// Also, all the package registrations owned by this user which match the 
        /// specified namespace only will be marked as unverifed.
        /// </summary>
        /// <param name="prefix">The reserved namespace to modify</param>
        /// <param name="username">The user to remove the ownership for the namespace</param>
        /// <returns>Awaitable Task</returns>
        Task DeleteOwnerFromReservedNamespaceAsync(string prefix, string username);

        ReservedNamespace FindReservedNamespaceForPrefix(string prefix);

        IReadOnlyCollection<ReservedNamespace> FindAllReservedNamespacesForPrefix(string prefix, bool getExactMatches);

        IReadOnlyCollection<ReservedNamespace> FindReservedNamespacesForPrefixList(IReadOnlyCollection<string> prefixList);

        IReadOnlyCollection<ReservedNamespace> GetReservedNamespacesForId(string id);
    }
}