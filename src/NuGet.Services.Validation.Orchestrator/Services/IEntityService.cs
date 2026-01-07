// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Interface to interact with the Gallery entities
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IEntityService<T> where T : class, IEntity
    {
        /// <summary>
        /// Find the entity based on the id and version.
        /// </summary>
        /// <param name="id">The id .</param>
        /// <param name="version">The version.</param>
        /// <returns>The entity.</returns>
        IValidatingEntity<T> FindPackageByIdAndVersionStrict(string id, string version);

        /// <summary>
        /// Find the entity based on the key.
        /// </summary>
        /// <returns>The entity.</returns>
        IValidatingEntity<T> FindPackageByKey(int key);

        /// <summary>
        /// Update the status of the entity.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="newStatus"></param>
        /// <param name="commitChanges"></param>
        /// <returns></returns>
        Task UpdateStatusAsync(T entity, PackageStatus newStatus, bool commitChanges = true);

        /// <summary>
        /// Update the entity metadata.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="metadata">The metadata.</param>
        /// <param name="commitChanges">True if the changes will be commited to the database.</param>
        /// <returns>A <see cref="Task"/> that can be used to await for the operation completion.</returns>
        Task UpdateMetadataAsync(T entity, object metadata, bool commitChanges = true);
    }
}
