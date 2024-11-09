// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Formats.Asn1;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 6960 (https://datatracker.ietf.org/doc/html/rfc6960#section-4.2.1):

           SingleResponse ::= SEQUENCE {
              certID                       CertID,
              certStatus                   CertStatus,
              thisUpdate                   GeneralizedTime,
              nextUpdate         [0]       EXPLICIT GeneralizedTime OPTIONAL,
              singleExtensions   [1]       EXPLICIT Extensions OPTIONAL }
    */
    internal sealed class SingleResponse
    {
        internal CertId CertId { get; }
        internal CertStatus CertStatus { get; }
        internal DateTimeOffset ThisUpdate { get; }
        internal DateTimeOffset? NextUpdate { get; }
        internal IReadOnlyList<X509ExtensionAsn>? SingleExtensions { get; }

        internal SingleResponse(
            CertId certId,
            CertStatus certStatus,
            DateTimeOffset thisUpdate,
            DateTimeOffset? nextUpdate,
            IReadOnlyList<X509ExtensionAsn>? singleExtensions)
        {
            if (certId is null)
            {
                throw new ArgumentNullException(nameof(certId));
            }

            if (certStatus is null)
            {
                throw new ArgumentNullException(nameof(certStatus));
            }

            CertId = certId;
            CertStatus = certStatus;
            ThisUpdate = thisUpdate;
            NextUpdate = nextUpdate;
            SingleExtensions = singleExtensions;
        }

        internal void Encode(AsnWriter writer)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            using (writer.PushSequence())
            {
                CertId.Encode(writer);
                CertStatus.Encode(writer);
                writer.WriteGeneralizedTime(ThisUpdate, omitFractionalSeconds: true);

                if (NextUpdate is not null)
                {
                    using (writer.PushSequence(Asn1Tags.ContextSpecific0))
                    {
                        writer.WriteGeneralizedTime(NextUpdate.Value, omitFractionalSeconds: true);
                    }
                }

                if (SingleExtensions?.Count > 0)
                {
                    using (writer.PushSequence(Asn1Tags.ContextSpecific1))
                    using (writer.PushSequence())
                    {
                        foreach (X509ExtensionAsn extension in SingleExtensions)
                        {
                            extension.Encode(writer);
                        }
                    }
                }
            }
        }
    }
}
