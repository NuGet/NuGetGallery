// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Formats.Asn1;
using System.Security.Cryptography;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 5652 (https://datatracker.ietf.org/doc/html/rfc5652#section-5.3):

          SignerInfo ::= SEQUENCE {
            version CMSVersion,
            sid SignerIdentifier,
            digestAlgorithm DigestAlgorithmIdentifier,
            signedAttrs [0] IMPLICIT SignedAttributes OPTIONAL,
            signatureAlgorithm SignatureAlgorithmIdentifier,
            signature SignatureValue,
            unsignedAttrs [1] IMPLICIT UnsignedAttributes OPTIONAL }

          SignerIdentifier ::= CHOICE {
            issuerAndSerialNumber IssuerAndSerialNumber,
            subjectKeyIdentifier [0] SubjectKeyIdentifier }

          SignedAttributes ::= SET SIZE (1..MAX) OF Attribute

          UnsignedAttributes ::= SET SIZE (1..MAX) OF Attribute

          Attribute ::= SEQUENCE {
            attrType OBJECT IDENTIFIER,
            attrValues SET OF AttributeValue }

          AttributeValue ::= ANY

          SignatureValue ::= OCTET STRING
    */
    public sealed class TestSignerInfo
    {
        private readonly ReadOnlyMemory<byte> _version;
        private readonly ReadOnlyMemory<byte> _sid;
        private readonly ReadOnlyMemory<byte> _digestAlgorithm;
        private readonly CryptographicAttributeObjectCollection? _signedAttrs;
        private readonly ReadOnlyMemory<byte> _signatureAlgorithm;
        private readonly ReadOnlyMemory<byte> _signature;
        private CryptographicAttributeObjectCollection? _unsignedAttrs;

        internal TestSignerInfo(
            ReadOnlyMemory<byte> version,
            ReadOnlyMemory<byte> sid,
            ReadOnlyMemory<byte> digestAlgorithm,
            CryptographicAttributeObjectCollection? signedAttrs,
            ReadOnlyMemory<byte> signatureAlgorithm,
            ReadOnlyMemory<byte> signature,
            CryptographicAttributeObjectCollection? unsignedAttrs)
        {
            _version = version;
            _sid = sid;
            _digestAlgorithm = digestAlgorithm;
            _signedAttrs = signedAttrs;
            _signatureAlgorithm = signatureAlgorithm;
            _signature = signature;
            _unsignedAttrs = unsignedAttrs;
        }

        public static TestSignerInfo Decode(ReadOnlyMemory<byte> bytes)
        {
            AsnReader reader = new(bytes, AsnEncodingRules.DER);

            return Decode(reader);
        }

        public static TestSignerInfo Decode(AsnReader reader)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            AsnReader sequenceReader = reader.ReadSequence();

            ReadOnlyMemory<byte> version = sequenceReader.ReadEncodedValue();
            ReadOnlyMemory<byte> sid = sequenceReader.ReadEncodedValue();
            ReadOnlyMemory<byte> digestAlgorithm = sequenceReader.ReadEncodedValue();
            CryptographicAttributeObjectCollection? signedAttrs = null;

            if (sequenceReader.PeekTag().HasSameClassAndValue(Asn1Tags.ContextSpecific0))
            {
                signedAttrs = new CryptographicAttributeObjectCollection();

                AsnReader signedAttrsReader = sequenceReader.ReadSetOf(Asn1Tags.ContextSpecific0);

                while (signedAttrsReader.HasData)
                {
                    CryptographicAttributeObject attribute = DecodeAttribute(signedAttrsReader);

                    signedAttrs.Add(attribute);
                }
            }

            ReadOnlyMemory<byte> signatureAlgorithm = sequenceReader.ReadEncodedValue();
            ReadOnlyMemory<byte> signature = sequenceReader.ReadEncodedValue();
            CryptographicAttributeObjectCollection? unsignedAttrs = null;

            if (sequenceReader.HasData && sequenceReader.PeekTag().HasSameClassAndValue(Asn1Tags.ContextSpecific1))
            {
                unsignedAttrs = new CryptographicAttributeObjectCollection();

                AsnReader unsignedAttrsReader = sequenceReader.ReadSetOf(Asn1Tags.ContextSpecific1);

                while (unsignedAttrsReader.HasData)
                {
                    CryptographicAttributeObject attribute = DecodeAttribute(unsignedAttrsReader);

                    unsignedAttrs.Add(attribute);
                }
            }

            return new TestSignerInfo(
                version,
                sid,
                digestAlgorithm,
                signedAttrs,
                signatureAlgorithm,
                signature,
                unsignedAttrs);
        }

        public void Encode(AsnWriter writer)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            using (writer.PushSequence())
            {
                writer.WriteEncodedValue(_version.Span);
                writer.WriteEncodedValue(_sid.Span);
                writer.WriteEncodedValue(_digestAlgorithm.Span);

                if (_signedAttrs?.Count > 0)
                {
                    using (writer.PushSetOf(Asn1Tags.ContextSpecific0))
                    {
                        foreach (CryptographicAttributeObject attribute in _signedAttrs)
                        {
                            EncodeAttribute(writer, attribute);
                        }
                    }
                }

                writer.WriteEncodedValue(_signatureAlgorithm.Span);
                writer.WriteEncodedValue(_signature.Span);

                if (_unsignedAttrs?.Count > 0)
                {
                    using (writer.PushSetOf(Asn1Tags.ContextSpecific1))
                    {
                        foreach (CryptographicAttributeObject attribute in _unsignedAttrs)
                        {
                            EncodeAttribute(writer, attribute);
                        }
                    }
                }
            }
        }

        public void AddUnsignedAttribute(CryptographicAttributeObject attribute)
        {
            if (attribute is null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }

            _unsignedAttrs ??= new CryptographicAttributeObjectCollection();

            _unsignedAttrs.Add(attribute);
        }

        public void RemoveUnsignedAttribute(Oid oid)
        {
            if (oid is null)
            {
                throw new ArgumentNullException(nameof(oid));
            }

            if (_unsignedAttrs is null || _unsignedAttrs.Count == 0)
            {
                return;
            }

            foreach (CryptographicAttributeObject attribute in _unsignedAttrs)
            {
                if (string.Equals(attribute.Oid.Value, oid.Value))
                {
                    _unsignedAttrs.Remove(attribute);

                    return;
                }
            }
        }

        public bool TryGetUnsignedAttribute(Oid oid, out CryptographicAttributeObject? attribute)
        {
            attribute = null;

            if (oid is null)
            {
                throw new ArgumentNullException(nameof(oid));
            }

            if (_unsignedAttrs is null || _unsignedAttrs.Count == 0)
            {
                return false;
            }

            foreach (CryptographicAttributeObject unsignedAttribute in _unsignedAttrs)
            {
                if (string.Equals(unsignedAttribute.Oid.Value, oid.Value))
                {
                    attribute = unsignedAttribute;

                    return true;
                }
            }

            return false;
        }

        private static CryptographicAttributeObject DecodeAttribute(AsnReader reader)
        {
            AsnReader sequenceReader = reader.ReadSequence();
            Oid oid = new(sequenceReader.ReadObjectIdentifier());
            AsnReader setReader = sequenceReader.ReadSetOf();
            AsnEncodedDataCollection values = new();

            while (setReader.HasData)
            {
                ReadOnlyMemory<byte> bytes = setReader.ReadEncodedValue();
                AsnEncodedData value = new(oid, bytes.Span.ToArray());

                values.Add(value);
            }

            return new CryptographicAttributeObject(oid, values);
        }

        private static void EncodeAttribute(AsnWriter writer, CryptographicAttributeObject attribute)
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(attribute.Oid.Value!);

                using (writer.PushSetOf())
                {
                    foreach (AsnEncodedData? value in attribute.Values)
                    {
                        writer.WriteEncodedValue(value.RawData);
                    }
                }
            }
        }
    }
}
