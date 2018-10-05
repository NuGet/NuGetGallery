// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Status.Table
{
    /// <summary>
    /// A <see cref="IComponentAffectingEntity"/> that is aggregated by a <typeparamref name="TAggregation"/>.
    /// In other words, a <see cref="IComponentAffectingEntity"/> that is also a <see cref="IChildEntity{TParent}"/>.
    /// </summary>
    public interface IAggregatedComponentAffectingEntity<TAggregation> : IChildEntity<TAggregation>, IComponentAffectingEntity
        where TAggregation : IComponentAffectingEntity
    {
    }
}
