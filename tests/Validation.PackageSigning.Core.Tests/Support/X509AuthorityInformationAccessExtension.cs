// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    internal sealed class X509AuthorityInformationAccessExtension : X509Extension
    {
        private static readonly string AuthorityInfoAccess = "1.3.6.1.5.5.7.1.1";
        private static readonly string Ocsp = "1.3.6.1.5.5.7.48.1";
        private static readonly string CaIssuers = "1.3.6.1.5.5.7.48.2";

        internal X509AuthorityInformationAccessExtension(Uri? ocspResponderUrl, Uri? caIssuersUrl)
            : base(AuthorityInfoAccess, Encode(ocspResponderUrl, caIssuersUrl), critical: false)
        {
        }

        private static byte[] Encode(Uri? ocspResponderUrl, Uri? caIssuersUrl)
        {
            AsnWriter writer = new(AsnEncodingRules.DER);

            using (writer.PushSequence())
            {
                if (ocspResponderUrl is not null)
                {
                    using (writer.PushSequence())
                    {
                        writer.WriteObjectIdentifier(Ocsp);
                        writer.WriteCharacterString(
                            UniversalTagNumber.IA5String,
                            ocspResponderUrl.OriginalString,
                            new Asn1Tag(TagClass.ContextSpecific, tagValue: 6));
                    }
                }

                if (caIssuersUrl is not null)
                {
                    using (writer.PushSequence())
                    {
                        writer.WriteObjectIdentifier(CaIssuers);
                        writer.WriteCharacterString(
                            UniversalTagNumber.IA5String,
                            caIssuersUrl.OriginalString,
                            new Asn1Tag(TagClass.ContextSpecific, tagValue: 6));
                    }
                }
            }

            return writer.Encode();
        }
    }
}
