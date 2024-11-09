// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Formats.Asn1;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 5280 (https://datatracker.ietf.org/doc/html/rfc5280#section-4.2.1.6):

           EDIPartyName ::= SEQUENCE {
                nameAssigner            [0]     DirectoryString OPTIONAL,
                partyName               [1]     DirectoryString }
    */
    public sealed class EdiPartyName
    {
        public DirectoryString? NameAssigner { get; }
        public DirectoryString PartyName { get; }

        private EdiPartyName(DirectoryString? nameAssigner, DirectoryString partyName)
        {
            NameAssigner = nameAssigner;
            PartyName = partyName;
        }

        public static EdiPartyName Decode(AsnReader reader, Asn1Tag? tag = null)
        {
            AsnReader sequenceReader = reader.ReadSequence(tag);
            DirectoryString? nameAssigner = null;

            if (sequenceReader.HasData && sequenceReader.PeekTag().HasSameClassAndValue(Asn1Tags.ContextSpecific0))
            {
                ReadOnlyMemory<byte> value = sequenceReader.ReadEncodedValue();

                nameAssigner = DirectoryString.Decode(sequenceReader, Asn1Tags.ContextSpecific0);
            }

            DirectoryString partyName = DirectoryString.Decode(sequenceReader, Asn1Tags.ContextSpecific1);

            sequenceReader.ThrowIfNotEmpty();

            return new EdiPartyName(nameAssigner, partyName);
        }

        public void Encode(AsnWriter writer)
        {
            Encode(writer, Asn1Tag.Sequence);
        }

        public void Encode(AsnWriter writer, Asn1Tag? tag = null)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            using (writer.PushSequence(tag))
            {
                if (NameAssigner is not null)
                {
                    NameAssigner.Encode(writer, Asn1Tags.ContextSpecific0);
                }

                PartyName.Encode(writer, Asn1Tags.ContextSpecific1);
            }
        }
    }
}
