// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Formats.Asn1;
using System.Security.Cryptography;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 5280 (https://datatracker.ietf.org/doc/html/rfc5280#section-4.1.2.4):

           DirectoryString ::= CHOICE {
                 teletexString           TeletexString (SIZE (1..MAX)),
                 printableString         PrintableString (SIZE (1..MAX)),
                 universalString         UniversalString (SIZE (1..MAX)),
                 utf8String              UTF8String (SIZE (1..MAX)),
                 bmpString               BMPString (SIZE (1..MAX)) }
    */
    public sealed class DirectoryString
    {
        internal string? TeletexString { get; }
        internal string? PrintableString { get; }
        internal ReadOnlyMemory<byte>? UniversalString { get; }
        internal string? Utf8String { get; }
        internal string? BmpString { get; }

#if DEBUG
        static DirectoryString()
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

            ensureUniqueTag(new Asn1Tag(UniversalTagNumber.T61String), "TeletexString");
            ensureUniqueTag(new Asn1Tag(UniversalTagNumber.PrintableString), "PrintableString");
            ensureUniqueTag(new Asn1Tag((UniversalTagNumber)28), "UniversalString");
            ensureUniqueTag(new Asn1Tag(UniversalTagNumber.UTF8String), "Utf8String");
            ensureUniqueTag(new Asn1Tag(UniversalTagNumber.BMPString), "BmpString");
        }
#endif

        private DirectoryString(
            string? teletexString = null,
            string? printableString = null,
            ReadOnlyMemory<byte>? universalString = null,
            string? utf8String = null,
            string? bmpString = null)
        {
            TeletexString = teletexString;
            PrintableString = printableString;
            UniversalString = universalString;
            Utf8String = utf8String;
            BmpString = bmpString;
        }

        public static DirectoryString Decode(AsnReader reader, Asn1Tag? expectedTag = null)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            if (expectedTag is not null)
            {
                throw new NotImplementedException();
            }

            Asn1Tag tag = reader.PeekTag();

            if (tag.HasSameClassAndValue(new Asn1Tag(UniversalTagNumber.T61String)))
            {
                string teletexString = reader.ReadCharacterString(UniversalTagNumber.T61String);

                return new DirectoryString(teletexString: teletexString);
            }
            else if (tag.HasSameClassAndValue(new Asn1Tag(UniversalTagNumber.PrintableString)))
            {
                string printableString = reader.ReadCharacterString(UniversalTagNumber.PrintableString);

                return new DirectoryString(printableString: printableString);
            }
            else if (tag.HasSameClassAndValue(new Asn1Tag((UniversalTagNumber)28)))
            {
                ReadOnlyMemory<byte> universalString = reader.ReadEncodedValue();

                return new DirectoryString(universalString: universalString);
            }
            else if (tag.HasSameClassAndValue(new Asn1Tag(UniversalTagNumber.UTF8String)))
            {
                string utf8String = reader.ReadCharacterString(UniversalTagNumber.UTF8String);

                return new DirectoryString(utf8String: utf8String);
            }
            else if (tag.HasSameClassAndValue(new Asn1Tag(UniversalTagNumber.BMPString)))
            {
                string bmpString = reader.ReadCharacterString(UniversalTagNumber.BMPString);

                return new DirectoryString(bmpString: bmpString);
            }
            else
            {
                throw new CryptographicException();
            }
        }

        public void Encode(AsnWriter writer)
        {
            Encode(writer, tag: null);
        }

        public void Encode(AsnWriter writer, Asn1Tag? tag = null)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (tag is not null)
            {
                throw new NotImplementedException();
            }

            bool wroteValue = false;

            if (TeletexString != null)
            {
                if (wroteValue)
                    throw new CryptographicException();

                writer.WriteCharacterString(UniversalTagNumber.T61String, TeletexString, tag);
                wroteValue = true;
            }

            if (PrintableString != null)
            {
                if (wroteValue)
                    throw new CryptographicException();

                writer.WriteCharacterString(UniversalTagNumber.PrintableString, PrintableString, tag);
                wroteValue = true;
            }

            if (UniversalString.HasValue)
            {
                if (wroteValue)
                    throw new CryptographicException();

                // Validator for tag constraint for UniversalString
                {
                    if (!Asn1Tag.TryDecode(UniversalString.Value.Span, out Asn1Tag validateTag, out _) ||
                        !validateTag.HasSameClassAndValue(new Asn1Tag((UniversalTagNumber)28)))
                    {
                        throw new CryptographicException();
                    }
                }

                try
                {
                    writer.WriteEncodedValue(UniversalString.Value.Span);
                }
                catch (ArgumentException e)
                {
                    throw new CryptographicException("ASN1 corrupted data.", e);
                }
                wroteValue = true;
            }

            if (Utf8String != null)
            {
                if (wroteValue)
                    throw new CryptographicException();

                writer.WriteCharacterString(UniversalTagNumber.UTF8String, Utf8String, tag);
                wroteValue = true;
            }

            if (BmpString != null)
            {
                if (wroteValue)
                    throw new CryptographicException();

                writer.WriteCharacterString(UniversalTagNumber.BMPString, BmpString, tag);
                wroteValue = true;
            }

            if (!wroteValue)
            {
                throw new CryptographicException();
            }
        }
    }
}
