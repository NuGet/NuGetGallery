// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Status.Table
{
    /// <summary>
    /// An entity that describes a period of time during which a component was affected.
    /// </summary>
    public interface IComponentAffectingEntity
    {
        /// <summary>
        /// The path to the component affected by this entity.
        /// </summary>
        string AffectedComponentPath { get; }

        /// <summary>
        /// The status of the component affected by this entity.
        /// </summary>
        /// <remarks>
        /// This should be a <see cref="ComponentStatus"/> converted to an enum.
        /// See https://github.com/Azure/azure-storage-net/issues/383
        /// </remarks>
        int AffectedComponentStatus { get; }

        /// <summary>
        /// The time that this entity began affecting a component.
        /// </summary>
        DateTime StartTime { get; }

        /// <summary>
        /// The time that this entity stopped affecting a component.
        /// <c>null</c> if the entity is currently affecting a component.
        /// </summary>
        DateTime? EndTime { get; }
        
        /// <summary>
        /// Whether or not this entity is currently affecting a component.
        /// </summary>
        bool IsActive { get; }
    }
}
