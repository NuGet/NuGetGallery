// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Status.Table
{
    /// <summary>
    /// An entity that has a parent.
    /// In other words, this entity has a many-to-one relationship between itself and a <typeparamref name="TParent"/> entity.
    /// </summary>
    public interface IChildEntity<TParent>
    {
        /// <summary>
        /// An identifier for the entity that is a parent of this entity.
        /// <c>null</c> if no such entity exists.
        /// </summary>
        string ParentRowKey { get; }

        /// <summary>
        /// Whether or not this entity has a parent.
        /// </summary>
        bool IsLinked { get; }
    }
}
