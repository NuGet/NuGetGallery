// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGet.Services.Validation.Orchestrator
{

    public interface IValidatingEntity<T> where T : class, IEntity
    {
        /// <summary>
        /// The Entity Key.
        /// </summary>
        int Key { get; }

        /// <summary>
        /// The <see cref="IEntity"/> to be validated.
        /// </summary>
        T EntityRecord { get; }

        /// <summary>
        /// The status of the entity. 
        /// </summary>
        PackageStatus Status { get; }

        /// <summary>
        /// The time when the entity was created in Gallery.
        /// </summary>
        DateTime Created { get; }

        /// <summary>
        /// The ValidatingType.
        /// </summary>
        ValidatingType ValidatingType { get; }
    }
}
