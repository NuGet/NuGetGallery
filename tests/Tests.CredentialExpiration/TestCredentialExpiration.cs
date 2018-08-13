// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gallery.CredentialExpiration;
using Gallery.CredentialExpiration.Models;

namespace Tests.CredentialExpiration
{
    public class TestCredentialExpiration : ICredentialExpirationExporter
    {
        private CredentialExpirationJobMetadata _jobMetadata;
        private DateTimeOffset _maxNotificationDate;
        private DateTimeOffset _minNotificationDate;
        
        private GalleryCredentialExpiration _galleryCredentialsExpiration;
        List<ExpiredCredentialData> _credentialSet;

        public TestCredentialExpiration(CredentialExpirationJobMetadata jobMetadata, List<ExpiredCredentialData> credentialSet)
        {
            _jobMetadata = jobMetadata;
            _credentialSet = credentialSet;
            _galleryCredentialsExpiration = new GalleryCredentialExpiration(null, jobMetadata);
            _maxNotificationDate = _galleryCredentialsExpiration.GetMaxNotificationDate();
            _minNotificationDate = _galleryCredentialsExpiration.GetMinNotificationDate();
        }
        public List<ExpiredCredentialData> GetExpiredCredentials(List<ExpiredCredentialData> credentialSet)
        {
            return _galleryCredentialsExpiration.GetExpiredCredentials(credentialSet);
        }

        public List<ExpiredCredentialData> GetExpiringCredentials(List<ExpiredCredentialData> credentialSet)
        {
            return _galleryCredentialsExpiration.GetExpiringCredentials(credentialSet);
        }

        public async Task<List<ExpiredCredentialData>> GetCredentialsAsync(TimeSpan timeout)
        {
            return await Task.FromResult(_credentialSet.Where( c => c.Expires >= GetMinNotificationDate() && c.Expires <= GetMaxNotificationDate()).ToList());
        }

        public DateTimeOffset GetMaxNotificationDate()
        {
            return _galleryCredentialsExpiration.GetMaxNotificationDate();
        }

        public DateTimeOffset GetMinNotificationDate()
        {
            return _galleryCredentialsExpiration.GetMinNotificationDate();
        }
    }
}
