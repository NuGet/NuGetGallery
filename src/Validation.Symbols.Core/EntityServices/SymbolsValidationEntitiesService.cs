//-----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------

using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Validation;

namespace NuGet.Jobs.Validation.Symbols.Core
{
    public class SymbolsValidationEntitiesService : ISymbolsValidationEntitiesService
    {
        private readonly IValidationEntitiesContext _validationEntitiesContext;

        public SymbolsValidationEntitiesService(IValidationEntitiesContext validationEntitiesContext)
        {
            _validationEntitiesContext = validationEntitiesContext ?? throw new ArgumentNullException(nameof(validationEntitiesContext));
        }

        public async Task<SymbolsServerRequest> AddSymbolsServerRequestAsync(SymbolsServerRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            var currentRequest = await GetSymbolsServerRequestAsync(request.RequestName, request.SymbolsKey);
            if (currentRequest != null)
            {
                return currentRequest;
            }

            _validationEntitiesContext.SymbolsServerRequests.Add(request);
            try
            {
                await _validationEntitiesContext.SaveChangesAsync();
                return request;
            }
            catch (DbUpdateException e) when (e.IsUniqueConstraintViolationException())
            {
                // The request must be ingested already.
                return await GetSymbolsServerRequestAsync(request.RequestName, request.SymbolsKey);
            }
        }

        public async Task<bool> TryUpdateSymbolsServerRequestAsync(SymbolsServerRequest request, SymbolsPackageIngestRequestStatus newStatus)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            if (await GetSymbolsServerRequestAsync(request.RequestName, request.SymbolsKey) == null)
            {
                return false;
            }
            request.RequestStatusKey = newStatus;
            request.LastUpdated = DateTime.UtcNow;
            await _validationEntitiesContext.SaveChangesAsync();
            return true;
        }

        public Task<SymbolsServerRequest> GetSymbolsServerRequestAsync(string requestName, int symbolsPackageKey)
        {
            return _validationEntitiesContext
               .SymbolsServerRequests
               .Where(s => (s.RequestName == requestName) && (s.SymbolsKey == symbolsPackageKey))
               .FirstOrDefaultAsync();
        }

        public async Task<SymbolsServerRequest> GetSymbolsServerRequestAsync(INuGetValidationRequest validationRequest)
        {
            string requestName = CreateSymbolServerRequestNameFromValidationRequest(validationRequest);
            return await GetSymbolsServerRequestAsync(requestName, validationRequest.PackageKey);
        }

        /// <summary>
        /// From a <see cref="INuGetValidationRequest"/> creates a symbol server request name.
        /// </summary>
        /// <param name="validationRequest"></param>
        /// <returns></returns>
        public static string CreateSymbolServerRequestNameFromValidationRequest(INuGetValidationRequest validationRequest)
        {
            return $"{validationRequest.PackageKey}_{validationRequest.ValidationId}";
        }

        /// <summary>
        /// Creates a <see cref="SymbolsServerRequest"/> from a <see cref="INuGetValidationRequest"/>
        /// </summary>
        /// <param name="validationRequest">The <see cref="INuGetValidationRequest"/>.</param>
        /// <param name="status">The <see cref="SymbolsPackageIngestRequestStatus"/>.</param>
        /// <returns></returns>
        public static SymbolsServerRequest CreateFromValidationRequest(
            INuGetValidationRequest validationRequest,
            SymbolsPackageIngestRequestStatus status,
            string requestName)
        {
            return new SymbolsServerRequest()
            {
                Created = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                RequestName = requestName,
                RequestStatusKey = status,
                SymbolsKey = validationRequest.PackageKey
            };
        }

        /// <summary>
        /// Converts a <see cref="SymbolsServerRequest"/> to <see cref="INuGetValidationResponse"/>
        /// </summary>
        /// <param name="symbolsServerRequest">A <see cref="SymbolsServerRequest" />.</param>
        /// <returns>The <see cref="INuGetValidationResponse"/>.</returns>
        public static INuGetValidationResponse ToValidationResponse(SymbolsServerRequest symbolsServerRequest)
        {
            if (symbolsServerRequest == null)
            {
                return new NuGetValidationResponse(ValidationStatus.NotStarted);
            }
            switch (symbolsServerRequest.RequestStatusKey)
            {
                case SymbolsPackageIngestRequestStatus.FailedIngestion:
                    return new NuGetValidationResponse(ValidationStatus.Failed);
                case SymbolsPackageIngestRequestStatus.Ingested:
                    return new NuGetValidationResponse(ValidationStatus.Succeeded);
                case SymbolsPackageIngestRequestStatus.Ingesting:
                    return new NuGetValidationResponse(ValidationStatus.Incomplete);
                default:
                    throw new ArgumentOutOfRangeException("Unexpected SymbolsPackageIngestRequestStatus.");
            }
        }
    }
}
