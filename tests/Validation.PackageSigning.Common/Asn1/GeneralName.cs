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

           GeneralName ::= CHOICE {
                otherName                       [0]     OtherName,
                rfc822Name                      [1]     IA5String,
                dNSName                         [2]     IA5String,
                x400Address                     [3]     ORAddress,
                directoryName                   [4]     Name,
                ediPartyName                    [5]     EDIPartyName,
                uniformResourceIdentifier       [6]     IA5String,
                iPAddress                       [7]     OCTET STRING,
                registeredID                    [8]     OBJECT IDENTIFIER }

           OtherName ::= SEQUENCE {
                type-id    OBJECT IDENTIFIER,
                value      [0] EXPLICIT ANY DEFINED BY type-id }

           EDIPartyName ::= SEQUENCE {
                nameAssigner            [0]     DirectoryString OPTIONAL,
                partyName               [1]     DirectoryString }
    */
    public sealed class GeneralName
    {
        public OtherName? OtherName { get; }
        public string? Rfc822Name { get; }
        public string? DnsName { get; }
        public ReadOnlyMemory<byte>? X400Address { get; }
        public ReadOnlyMemory<byte>? DirectoryName { get; }
        public EdiPartyName? EdiPartyName { get; }
        public string? Uri { get; }
        public ReadOnlyMemory<byte>? IPAddress { get; }
        public string? RegisteredId { get; }

#if DEBUG
        static GeneralName()
        {
            var usedTags = new System.Collections.Generic.Dictionary<Asn1Tag, string>();
            Action<Asn1Tag, string> ensureUniqueTag = (tag, fieldName) =>
            {
                if (usedTags.TryGetValue(tag, out string? existing))
                {
                    throw new InvalidOperationException($"Tag '{tag}' is in use by both '{existing}' and '{fieldName}'");
                }

                usedTags.Add(tag, fieldName);
            };

            ensureUniqueTag(Asn1Tags.ContextSpecific0, "OtherName");
            ensureUniqueTag(Asn1Tags.ContextSpecific1, "Rfc822Name");
            ensureUniqueTag(Asn1Tags.ContextSpecific2, "DnsName");
            ensureUniqueTag(Asn1Tags.ContextSpecific3, "X400Address");
            ensureUniqueTag(Asn1Tags.ContextSpecific4, "DirectoryName");
            ensureUniqueTag(Asn1Tags.ContextSpecific5, "EdiPartyName");
            ensureUniqueTag(Asn1Tags.ContextSpecific6, "Uri");
            ensureUniqueTag(Asn1Tags.ContextSpecific7, "IPAddress");
            ensureUniqueTag(Asn1Tags.ContextSpecific8, "RegisteredId");
        }
#endif

        public GeneralName(
            OtherName? otherName = null,
            string? rfc822Name = null,
            string? dnsName = null,
            ReadOnlyMemory<byte>? x400Address = null,
            ReadOnlyMemory<byte>? directoryName = null,
            EdiPartyName? ediPartyName = null,
            string? uri = null,
            ReadOnlyMemory<byte>? ipAddress = null,
            string? registeredId = null)
        {
            OtherName = otherName;
            Rfc822Name = rfc822Name;
            DnsName = dnsName;
            X400Address = x400Address;
            DirectoryName = directoryName;
            EdiPartyName = ediPartyName;
            Uri = uri;
            IPAddress = ipAddress;
            RegisteredId = registeredId;
        }

        public static GeneralName Decode(AsnReader reader)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            Asn1Tag tag = reader.PeekTag();
            AsnReader explicitReader;
            ReadOnlyMemory<byte> tmpSpan;

            if (tag.HasSameClassAndValue(Asn1Tags.ContextSpecific0))
            {
                OtherName otherName = OtherName.Decode(reader, Asn1Tags.ContextSpecific0);

                return new GeneralName(otherName: otherName);
            }
            else if (tag.HasSameClassAndValue(Asn1Tags.ContextSpecific1))
            {
                string rfc822Name = reader.ReadCharacterString(UniversalTagNumber.IA5String, Asn1Tags.ContextSpecific1);

                return new GeneralName(rfc822Name: rfc822Name);
            }
            else if (tag.HasSameClassAndValue(Asn1Tags.ContextSpecific2))
            {
                string dnsName = reader.ReadCharacterString(UniversalTagNumber.IA5String, Asn1Tags.ContextSpecific2);

                return new GeneralName(dnsName: dnsName);
            }
            else if (tag.HasSameClassAndValue(Asn1Tags.ContextSpecific3))
            {
                ReadOnlyMemory<byte> x400Address = reader.ReadEncodedValue();

                return new GeneralName(x400Address: x400Address);
            }
            else if (tag.HasSameClassAndValue(Asn1Tags.ContextSpecific4))
            {
                explicitReader = reader.ReadSequence(Asn1Tags.ContextSpecific4);

                ReadOnlyMemory<byte> directoryName = explicitReader.ReadEncodedValue();

                explicitReader.ThrowIfNotEmpty();

                return new GeneralName(directoryName: directoryName);
            }
            else if (tag.HasSameClassAndValue(Asn1Tags.ContextSpecific5))
            {
                explicitReader = reader.ReadSequence(Asn1Tags.ContextSpecific5);

                EdiPartyName ediPartyName = EdiPartyName.Decode(explicitReader);

                return new GeneralName(ediPartyName: ediPartyName);
            }
            else if (tag.HasSameClassAndValue(Asn1Tags.ContextSpecific6))
            {
                string uri = reader.ReadCharacterString(UniversalTagNumber.IA5String, Asn1Tags.ContextSpecific6);

                return new GeneralName(uri: uri);
            }
            else if (tag.HasSameClassAndValue(Asn1Tags.ContextSpecific7))
            {
                byte[] ipAddress;

                if (reader.TryReadPrimitiveOctetString(out tmpSpan, Asn1Tags.ContextSpecific7))
                {
                    ipAddress = tmpSpan.ToArray();
                }
                else
                {
                    ipAddress = reader.ReadOctetString(Asn1Tags.ContextSpecific7);
                }

                return new GeneralName(ipAddress: ipAddress);
            }
            else if (tag.HasSameClassAndValue(Asn1Tags.ContextSpecific8))
            {
                string registeredId = reader.ReadObjectIdentifier(Asn1Tags.ContextSpecific8);

                return new GeneralName(registeredId: registeredId);
            }
            else
            {
                throw new InvalidAsn1Exception();
            }
        }

        public void Encode(AsnWriter writer, Asn1Tag? tag = null)
        {
            bool wroteValue = false;

            if (OtherName is not null)
            {
                if (wroteValue)
                    throw new CryptographicException();

                OtherName.Encode(writer, Asn1Tags.ContextSpecific0);
                wroteValue = true;
            }

            if (Rfc822Name != null)
            {
                if (wroteValue)
                    throw new CryptographicException();

                writer.WriteCharacterString(UniversalTagNumber.IA5String, Rfc822Name, Asn1Tags.ContextSpecific1);
                wroteValue = true;
            }

            if (DnsName != null)
            {
                if (wroteValue)
                    throw new CryptographicException();

                writer.WriteCharacterString(UniversalTagNumber.IA5String, DnsName, Asn1Tags.ContextSpecific2);
                wroteValue = true;
            }

            if (X400Address.HasValue)
            {
                if (wroteValue)
                    throw new CryptographicException();

                // Validator for tag constraint for X400Address
                {
                    if (!Asn1Tag.TryDecode(X400Address.Value.Span, out Asn1Tag validateTag, out _) ||
                        !validateTag.HasSameClassAndValue(Asn1Tags.ContextSpecific3))
                    {
                        throw new CryptographicException();
                    }
                }

                try
                {
                    writer.WriteEncodedValue(X400Address.Value.Span);
                }
                catch (ArgumentException e)
                {
                    throw new CryptographicException("ASN1 corrupted data.", e);
                }
                wroteValue = true;
            }

            if (DirectoryName.HasValue)
            {
                if (wroteValue)
                    throw new CryptographicException();

                writer.PushSequence(Asn1Tags.ContextSpecific4);
                try
                {
                    writer.WriteEncodedValue(DirectoryName.Value.Span);
                }
                catch (ArgumentException e)
                {
                    throw new CryptographicException("ASN1 corrupted data.", e);
                }
                writer.PopSequence(Asn1Tags.ContextSpecific4);
                wroteValue = true;
            }

            if (EdiPartyName is not null)
            {
                if (wroteValue)
                    throw new CryptographicException();

                EdiPartyName.Encode(writer, Asn1Tags.ContextSpecific5);
                wroteValue = true;
            }

            if (Uri != null)
            {
                if (wroteValue)
                    throw new CryptographicException();

                writer.WriteCharacterString(UniversalTagNumber.IA5String, Uri, Asn1Tags.ContextSpecific6);
                wroteValue = true;
            }

            if (IPAddress.HasValue)
            {
                if (wroteValue)
                    throw new CryptographicException();

                writer.WriteOctetString(IPAddress.Value.Span, Asn1Tags.ContextSpecific7);
                wroteValue = true;
            }

            if (RegisteredId != null)
            {
                if (wroteValue)
                    throw new CryptographicException();

                try
                {
                    writer.WriteObjectIdentifier(RegisteredId, Asn1Tags.ContextSpecific8);
                }
                catch (ArgumentException e)
                {
                    throw new CryptographicException("ASN1 corrupted data.", e);
                }
                wroteValue = true;
            }

            if (!wroteValue)
            {
                throw new CryptographicException();
            }
        }
    }
}
