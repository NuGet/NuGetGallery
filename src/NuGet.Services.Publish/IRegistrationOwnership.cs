// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Ownership;

namespace NuGet.Services.Publish
{
    public interface IRegistrationOwnership
    {
        bool IsAuthenticated { get; }
        Task<bool> IsUserAdministrator();

        Task EnableTenant();
        Task DisableTenant();

        Task<bool> HasTenantEnabled();

        Task AddVersion(string ns, string id, string version);

        Task<bool> HasOwner(string ns, string id);
        Task<bool> HasRegistration(string ns, string id);
        Task<bool> HasVersion(string ns, string id, string version);
        Task<bool> HasNamespace(string ns);

        string GetUserId();
        Task<string> GetUserName();

        string GetTenantId();
        Task<string> GetTenantName();

        Task<IEnumerable<string>> GetDomains();
        Task<IEnumerable<string>> GetTenants();

        Task<string> GetPublisherName();
        Task<AgreementRecord> GetAgreement(string agreement, string agreementVersion);
        Task<AgreementRecord> AcceptAgreement(string agreement, string agreementVersion, string email);
    }
}