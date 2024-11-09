// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable SA1028 // ignore whitespace warnings for generated code
#pragma warning disable IDE1006 // Naming Styles
using System;
using System.Formats.Asn1;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    [StructLayout(LayoutKind.Sequential)]
    internal partial struct X509ExtensionAsn
    {
        private static ReadOnlyMemory<byte> DefaultCritical = new([0x01, 0x01, 0x00]);

        internal string ExtnId;
        internal bool Critical;
        internal ReadOnlyMemory<byte> ExtnValue;

#if DEBUG
        static X509ExtensionAsn()
        {
            X509ExtensionAsn decoded = default;
            AsnReader reader = new(DefaultCritical, AsnEncodingRules.DER);
            decoded.Critical = reader.ReadBoolean();
            reader.ThrowIfNotEmpty();
        }
#endif

        internal readonly void Encode(AsnWriter writer)
        {
            Encode(writer, Asn1Tag.Sequence);
        }

        internal readonly void Encode(AsnWriter writer, Asn1Tag tag)
        {
            writer.PushSequence(tag);

            try
            {
                writer.WriteObjectIdentifier(ExtnId);
            }
            catch (ArgumentException e)
            {
                throw new CryptographicException("ASN1 corrupted data.", e);
            }

            // DEFAULT value handler for Critical.
            {
                AsnWriter tmp = new(AsnEncodingRules.DER);
                tmp.WriteBoolean(Critical);

                if (!tmp.EncodedValueEquals(DefaultCritical.Span))
                {
                    tmp.CopyTo(writer);
                }
            }

            writer.WriteOctetString(ExtnValue.Span);
            writer.PopSequence(tag);
        }

        internal static X509ExtensionAsn Decode(AsnReader reader)
        {
            Decode(ref reader, rebind: default, out X509ExtensionAsn decoded);

            return decoded;
        }

        internal static X509ExtensionAsn Decode(ReadOnlyMemory<byte> encoded, AsnEncodingRules ruleSet)
        {
            return Decode(Asn1Tag.Sequence, encoded, ruleSet);
        }

        internal static X509ExtensionAsn Decode(Asn1Tag expectedTag, ReadOnlyMemory<byte> encoded, AsnEncodingRules ruleSet)
        {
            try
            {
                AsnReader reader = new(encoded, ruleSet);

                DecodeCore(ref reader, expectedTag, encoded, out X509ExtensionAsn decoded);
                reader.ThrowIfNotEmpty();
                return decoded;
            }
            catch (AsnContentException e)
            {
                throw new CryptographicException("ASN1 corrupted data.", e);
            }
        }

        internal static void Decode(ref AsnReader reader, ReadOnlyMemory<byte> rebind, out X509ExtensionAsn decoded)
        {
            Decode(ref reader, Asn1Tag.Sequence, rebind, out decoded);
        }

        internal static void Decode(ref AsnReader reader, Asn1Tag expectedTag, ReadOnlyMemory<byte> rebind, out X509ExtensionAsn decoded)
        {
            try
            {
                DecodeCore(ref reader, expectedTag, rebind, out decoded);
            }
            catch (AsnContentException e)
            {
                throw new CryptographicException("ASN1 corrupted data.", e);
            }
        }

        private static void DecodeCore(ref AsnReader reader, Asn1Tag expectedTag, ReadOnlyMemory<byte> rebind, out X509ExtensionAsn decoded)
        {
            decoded = default;
            AsnReader sequenceReader = reader.ReadSequence(expectedTag);
            AsnReader defaultReader;
            int offset;
            ReadOnlyMemory<byte> tmp;

            decoded.ExtnId = sequenceReader.ReadObjectIdentifier();

            if (sequenceReader.HasData && sequenceReader.PeekTag().HasSameClassAndValue(Asn1Tag.Boolean))
            {
                decoded.Critical = sequenceReader.ReadBoolean();
            }
            else
            {
                defaultReader = new AsnReader(DefaultCritical, AsnEncodingRules.DER);
                decoded.Critical = defaultReader.ReadBoolean();
            }


            if (sequenceReader.TryReadPrimitiveOctetString(out tmp))
            {
                decoded.ExtnValue = rebind.Span.Overlaps(tmp.Span, out offset) ? rebind.Slice(offset, tmp.Length) : tmp.ToArray();
            }
            else
            {
                decoded.ExtnValue = sequenceReader.ReadOctetString();
            }


            sequenceReader.ThrowIfNotEmpty();
        }
    }
}
