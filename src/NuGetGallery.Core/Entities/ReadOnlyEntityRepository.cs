// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace NuGetGallery
{
    public class ReadOnlyEntityRepository<T>(IReadOnlyEntitiesContext readOnlyEntitiesContext) : IReadOnlyEntityRepository<T>
        where T : class, new()
    {
        private readonly IReadOnlyEntitiesContext _readOnlyEntitiesContext = readOnlyEntitiesContext;

        public IQueryable<T> GetAll() => _readOnlyEntitiesContext.Set<T>();
    }
}