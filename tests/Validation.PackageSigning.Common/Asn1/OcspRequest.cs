// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Formats.Asn1;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 6960 (https://datatracker.ietf.org/doc/html/rfc6960#section-4.1):

           OCSPRequest     ::=     SEQUENCE {
               tbsRequest                  TBSRequest,
               optionalSignature   [0]     EXPLICIT Signature OPTIONAL }
    */
    internal sealed class OcspRequest
    {
        internal TbsRequest TbsRequest { get; }
        internal OcspSignature? OptionalSignature { get; }

        private OcspRequest(
            TbsRequest tbsRequest,
            OcspSignature? optionalSignature)
        {
            TbsRequest = tbsRequest;
            OptionalSignature = optionalSignature;
        }

        internal static OcspRequest Decode(ReadOnlyMemory<byte> bytes)
        {
            AsnReader reader = new(bytes, AsnEncodingRules.DER);

            return Decode(reader);
        }

        private static OcspRequest Decode(AsnReader reader)
        {
            AsnReader sequenceReader = reader.ReadSequence();
            TbsRequest tbsRequest = TbsRequest.Decode(sequenceReader);
            OcspSignature? optionalSignature = null;

            if (sequenceReader.HasData)
            {
                if (!sequenceReader.PeekTag().HasSameClassAndValue(Asn1Tags.ContextSpecific0))
                {
                    throw new InvalidAsn1Exception();
                }

                AsnReader signatureReader = sequenceReader.ReadSequence(Asn1Tags.ContextSpecific0);

                optionalSignature = OcspSignature.Decode(signatureReader);
            }

            sequenceReader.ThrowIfNotEmpty();

            return new OcspRequest(tbsRequest, optionalSignature);
        }
    }
}
