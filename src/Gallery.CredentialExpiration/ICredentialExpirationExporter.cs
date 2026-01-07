// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gallery.CredentialExpiration.Models;

namespace Gallery.CredentialExpiration
{
    public interface ICredentialExpirationExporter
    {
        /// <summary>
        /// Returns the entire credential set to work with.
        /// </summary>
        /// <param name="credentialSet"></param>
        /// <returns></returns>
        Task<List<ExpiredCredentialData>> GetCredentialsAsync(TimeSpan timeout);

        /// <summary>
        /// Returns the set of expired credentials that will have notification emails sent.
        /// </summary>
        /// <param name="credentialSet"></param>
        /// <returns></returns>
        List<ExpiredCredentialData> GetExpiredCredentials(List<ExpiredCredentialData> credentialSet);

        /// <summary>
        /// Returns the set of expiring credentials that will have notification emails sent.
        /// </summary>
        /// <param name="credentialSet"></param>
        /// <returns></returns>
        List<ExpiredCredentialData> GetExpiringCredentials(List<ExpiredCredentialData> credentialSet);
    }
}
