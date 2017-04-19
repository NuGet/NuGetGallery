// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using System.Web;

namespace NuGetGallery
{
    public interface ISecurityPolicyService
    {
        Task AddSecurityPolicyAsync(User user, SecurityPolicy securityPolicy);

        Task<SecurityPolicyResult> CanCreatePackageAsync(User user, HttpContextBase context, string id, string version);

        Task<SecurityPolicyResult> CanVerifyPackageKeyAsync(User user, HttpContextBase context, string id, string version);
    }
}
