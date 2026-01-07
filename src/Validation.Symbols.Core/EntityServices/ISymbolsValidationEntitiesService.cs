
//-----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------


using System.Threading.Tasks;
using NuGet.Services.Validation;

namespace NuGet.Jobs.Validation.Symbols.Core
{
    /// <summary>
    /// Interface that interacts with Validation db.
    /// </summary>
    public interface ISymbolsValidationEntitiesService
    {
        /// <summary>
        /// Will add a new request in the SymbolsServerRequests db
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The request ingested. If the request already exists the request that is persisted is returned.</returns>
        Task<SymbolsServerRequest> AddSymbolsServerRequestAsync(SymbolsServerRequest request);

        /// <summary>
        /// Try to update the status.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="newStatus">The new status.</param>
        /// <returns>True if success, false if the request is not in the database. Any exceptions will be propagated to the caller.</returns>
        Task<bool> TryUpdateSymbolsServerRequestAsync(SymbolsServerRequest request, SymbolsPackageIngestRequestStatus newStatus);

        /// <summary>
        /// Queries and returns the <see cref="SymbolsServerRequest"/> based on the requestname and symbolsPackageKey.
        /// </summary>
        /// <param name="requestName">The request name.</param>
        /// <param name="symbolsPackageKey">The key of the symbols package</param>
        /// <returns>The <see cref="SymbolsServerRequest"/> if found. Null otherwise.</returns>
        Task<SymbolsServerRequest> GetSymbolsServerRequestAsync(string requestName, int symbolsPackageKey);

        /// <summary>
        /// Queries and returns the <see cref="SymbolsServerRequest"/> based on the <see cref="INuGetValidationRequest"/> information.
        /// </summary>
        /// <param name="validationRequest">The validation request.</param>
        /// <returns>The result transformed to <see cref="SymbolsServerRequest"/>.</returns>
        Task<SymbolsServerRequest> GetSymbolsServerRequestAsync(INuGetValidationRequest validationRequest);
    }
}
