// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 6960 (https://datatracker.ietf.org/doc/html/rfc6960#section-4.2.1):

           RevokedInfo ::= SEQUENCE {
               revocationTime              GeneralizedTime,
               revocationReason    [0]     EXPLICIT CRLReason OPTIONAL }
    */
    internal sealed class RevokedInfo
    {
        internal DateTimeOffset RevocationTime { get; }
        internal X509RevocationReason? RevocationReason { get; }

        internal RevokedInfo(DateTimeOffset revocationTime, X509RevocationReason? revocationReason = null)
        {
            RevocationTime = revocationTime;
            RevocationReason = revocationReason;
        }

        internal void Encode(AsnWriter writer, Asn1Tag? tag)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            using (writer.PushSequence(tag))
            {
                writer.WriteGeneralizedTime(RevocationTime, omitFractionalSeconds: true);

                if (RevocationReason is not null)
                {
                    using (writer.PushSequence(Asn1Tags.ContextSpecific0))
                    {
                        writer.WriteEnumeratedValue(RevocationReason.Value);
                    }
                }
            }
        }
    }
}
