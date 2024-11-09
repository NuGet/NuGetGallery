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
        internal X509AuthorityInformationAccessExtension(Uri? ocspResponderUrl, Uri? caIssuersUrl)
            : base(TestOids.AuthorityInfoAccess.Value!, Encode(ocspResponderUrl, caIssuersUrl), critical: false)
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
                        writer.WriteObjectIdentifier(TestOids.Ocsp.Value!);
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
                        writer.WriteObjectIdentifier(TestOids.CaIssuers.Value!);
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
