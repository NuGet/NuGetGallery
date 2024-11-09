// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Formats.Asn1;
using System.Security.Cryptography;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 5280 (https://datatracker.ietf.org/doc/html/rfc5280#appendix-A.2):

            PolicyQualifierInfo ::= SEQUENCE {
                policyQualifierId  PolicyQualifierId,
                qualifier          ANY DEFINED BY policyQualifierId }

            -- policyQualifierIds for Internet policy qualifiers

            id-qt          OBJECT IDENTIFIER ::=  { id-pkix 2 }
            id-qt-cps      OBJECT IDENTIFIER ::=  { id-qt 1 }
            id-qt-unotice  OBJECT IDENTIFIER ::=  { id-qt 2 }

            PolicyQualifierId ::= OBJECT IDENTIFIER ( id-qt-cps | id-qt-unotice )
    */
    public sealed class PolicyQualifierInfo
    {
        public Oid PolicyQualifierId { get; }
        public ReadOnlyMemory<byte>? Qualifier { get; }

        public PolicyQualifierInfo(Oid policyQualifierId, ReadOnlyMemory<byte>? qualifier)
        {
            PolicyQualifierId = policyQualifierId;
            Qualifier = qualifier;
        }

        public static PolicyQualifierInfo Decode(AsnReader reader)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            AsnReader sequenceReader = reader.ReadSequence();
            Oid policyQualifierId = new(sequenceReader.ReadObjectIdentifier());
            ReadOnlyMemory<byte>? qualifier = null;

            if (sequenceReader.HasData)
            {
                qualifier = sequenceReader.ReadEncodedValue();
            }

            sequenceReader.ThrowIfNotEmpty();

            return new PolicyQualifierInfo(policyQualifierId, qualifier);
        }

        public void Encode(AsnWriter writer)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(PolicyQualifierId.Value!);

                if (Qualifier.HasValue)
                {
                    writer.WriteEncodedValue(Qualifier.Value.Span);
                }
            }
        }
    }
}
