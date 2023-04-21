// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.Login
{
    public class LoginDiscontinuationReference
    {
        public LoginDiscontinuationReference(LoginDiscontinuation loginDiscontinuation, string contentId)
        {
            if (string.IsNullOrEmpty(contentId))
            {
                throw new ArgumentException(nameof(contentId));
            }

            Logins = loginDiscontinuation ?? throw new ArgumentException(nameof(loginDiscontinuation));
            ContentId = contentId;
        }

        /// <summary>
        /// The login discontinuation's content, serialized as JSON.
        /// </summary>
        public LoginDiscontinuation Logins { get; }

        /// <summary>
        /// The login discontinuation's ETag.
        /// </summary>
        public string ContentId { get; }
    }
}
