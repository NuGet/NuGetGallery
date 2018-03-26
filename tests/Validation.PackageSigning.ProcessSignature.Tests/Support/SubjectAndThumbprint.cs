// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Validation.PackageSigning.ProcessSignature.Tests
{
    /// <summary>
    /// A certificate subject and thumbprint. The subject is for human-readability and the thumbprint is lowercase
    /// hexadecimal SHA-256.
    /// </summary>
    public class SubjectAndThumbprint
    {
        public SubjectAndThumbprint(string subject, string thumbprint)
        {
            Subject = subject;
            Thumbprint = thumbprint;
        }

        public string Subject { get; }
        public string Thumbprint { get; }
    }
}
