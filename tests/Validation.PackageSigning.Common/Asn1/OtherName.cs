// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Formats.Asn1;
using System.Security.Cryptography;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 5280 (https://datatracker.ietf.org/doc/html/rfc5280#section-4.2.1.6):

           OtherName ::= SEQUENCE {
                type-id    OBJECT IDENTIFIER,
                value      [0] EXPLICIT ANY DEFINED BY type-id }
    */
    public sealed class OtherName
    {
        public Oid TypeId { get; }
        public ReadOnlyMemory<byte> Value { get; }

        private OtherName(Oid typeId, ReadOnlyMemory<byte> value)
        {
            TypeId = typeId;
            Value = value;
        }

        public static OtherName Decode(AsnReader reader, Asn1Tag? tag = null)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            AsnReader sequenceReader = reader.ReadSequence(tag);
            Oid typeId = new(sequenceReader.ReadObjectIdentifier());
            AsnReader explicitReader = sequenceReader.ReadSequence(Asn1Tags.ContextSpecific0);
            ReadOnlyMemory<byte> value = explicitReader.ReadEncodedValue();

            return new OtherName(typeId, value);
        }

        internal void Encode(AsnWriter writer)
        {
            Encode(writer, Asn1Tag.Sequence);
        }

        internal void Encode(AsnWriter writer, Asn1Tag? tag = null)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            using (writer.PushSequence(tag))
            {
                writer.WriteObjectIdentifier(TypeId.Value!);

                using (writer.PushSequence(Asn1Tags.ContextSpecific0))
                {
                    writer.WriteEncodedValue(Value.Span);
                }
            }
        }
    }
}
