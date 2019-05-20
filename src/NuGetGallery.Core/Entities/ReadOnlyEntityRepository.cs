// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace NuGetGallery
{
    public class ReadOnlyEntityRepository<T>
        : IReadOnlyEntityRepository<T>
        where T : class, new()
    {
        private readonly IReadOnlyEntitiesContext _readOnlyEntitiesContext;

        public ReadOnlyEntityRepository(IReadOnlyEntitiesContext readOnlyEntitiesContext)
        {
            _readOnlyEntitiesContext = readOnlyEntitiesContext;
        }

        public IQueryable<T> GetAll()
        {
            return _readOnlyEntitiesContext.Set<T>();
        }
    }
}