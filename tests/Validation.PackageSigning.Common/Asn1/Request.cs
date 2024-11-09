// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Formats.Asn1;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 6960 (https://datatracker.ietf.org/doc/html/rfc6960#section-4.1):

           Request         ::=     SEQUENCE {
               reqCert                     CertID,
               singleRequestExtensions     [0] EXPLICIT Extensions OPTIONAL }
    */
    internal sealed class Request
    {
        internal CertId CertId { get; }
        internal IReadOnlyList<X509ExtensionAsn> SingleRequestExtensions { get; }

        private Request(
            CertId certId,
            IReadOnlyList<X509ExtensionAsn> singleRequestExtensions)
        {
            CertId = certId;
            SingleRequestExtensions = singleRequestExtensions;
        }

        internal static Request Decode(AsnReader reader)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            AsnReader sequenceReader = reader.ReadSequence();
            CertId certId = CertId.Decode(sequenceReader);
            List<X509ExtensionAsn> singleRequestExtensions = new();

            if (sequenceReader.HasData)
            {
                Asn1Tag context0 = new(TagClass.ContextSpecific, 0);

                if (sequenceReader.PeekTag().HasSameClassAndValue(context0))
                {
                    AsnReader extensionsSequenceReader = sequenceReader.ReadSequence();

                    while (extensionsSequenceReader.HasData)
                    {
                        X509ExtensionAsn.Decode(ref extensionsSequenceReader, rebind: default, out X509ExtensionAsn decoded);

                        singleRequestExtensions.Add(decoded);
                    }
                }
            }

            return new Request(certId, singleRequestExtensions);
        }
    }
}
