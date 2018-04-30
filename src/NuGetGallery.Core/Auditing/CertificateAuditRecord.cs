// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.Auditing
{
    public sealed class CertificateAuditRecord : AuditRecord<AuditedCertificateAction>
    {
        public string Thumbprint { get; }
        public string HashAlgorithm => "SHA-256";

        public CertificateAuditRecord(AuditedCertificateAction action, string thumbprint)
            : base(action)
        {
            if (string.IsNullOrEmpty(thumbprint))
            {
                throw new ArgumentException(CoreStrings.ArgumentCannotBeNullOrEmpty, nameof(thumbprint));
            }

            if (thumbprint.Length != 64) // Did the thumbprint hash algorithm change?
            {
                throw new ArgumentException(CoreStrings.CertificateThumbprintHashAlgorithmChanged, nameof(thumbprint));
            }

            Thumbprint = thumbprint.ToLowerInvariant();
        }

        public override string GetPath()
        {
            return Thumbprint;
        }
    }
}