// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGetGallery.Auditing
{
    public sealed class CertificatePatternAuditRecord : AuditRecord<AuditedCertificatePatternAction>
    {
        public CertificatePatternType PatternType { get; }
        public string Identifier { get; }

        public CertificatePatternAuditRecord(AuditedCertificatePatternAction action, CertificatePatternType patternType, string identifier)
            : base(action)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException(CoreStrings.ArgumentCannotBeNullOrEmpty, nameof(identifier));
            }

            PatternType = patternType;
            Identifier = identifier;
        }

        public override string GetPath()
        {
            return $"{PatternType}/{Identifier}";
        }
    }
}