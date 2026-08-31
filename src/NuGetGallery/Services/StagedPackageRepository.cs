// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class StagedPackageRepository : EntityRepository<StagedPackage>, IStagedPackageRepository
    {
        private readonly IEntitiesContext _entitiesContext;

        public StagedPackageRepository(IEntitiesContext entitiesContext)
            : base(entitiesContext)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
        }

        public async Task ExecuteInTransactionAsync(Func<Task> action)
        {
            action = action ?? throw new ArgumentNullException(nameof(action));

            using (new SuspendDbExecutionStrategy())
            using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
            {
                await action();
                transaction.Commit();
            }
        }
    }
}
