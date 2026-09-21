// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGetGallery;

namespace NuGet.Services.Staging.Promotion
{
    /// <summary>
    /// Acquires transaction-scoped SQL locks for staging group state transitions.
    /// </summary>
    public class StagingGroupLockService : IStagingGroupLockService
    {
        private readonly IEntitiesContext _entitiesContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="StagingGroupLockService"/> class.
        /// </summary>
        /// <param name="entitiesContext">The entities context associated with the caller's transaction.</param>
        public StagingGroupLockService(IEntitiesContext entitiesContext)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
        }

        /// <inheritdoc />
        public Task AcquireAsync(int stagingGroupKey)
        {
            // This SELECT is only used to lock the group row. Another package completing for the same group
            // must wait until the current transaction finishes, then it can read the latest member statuses.
            return _entitiesContext.GetDatabase().ExecuteSqlCommandAsync(
                @"SELECT [Key]
                  FROM [dbo].[StagingGroups] WITH (UPDLOCK, HOLDLOCK)
                  WHERE [Key] = @p0",
                stagingGroupKey);
        }
    }
}
