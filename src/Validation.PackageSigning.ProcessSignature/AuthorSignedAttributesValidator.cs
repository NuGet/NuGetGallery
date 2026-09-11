// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Formats.Asn1;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    // Traverses the CMS structures defined by RFC 5652, sections 3 and 5, and validates the
    // signedAttrs ordering requirement described in sections 5.3 and 5.4.
    internal static class AuthorSignedAttributesValidator
    {
        private const string SignedDataOid = "1.2.840.113549.1.7.2";
        private static readonly Asn1Tag ContextSpecific0 = new(tagClass: TagClass.ContextSpecific, tagValue: 0);
        private static readonly Asn1Tag ContextSpecific1 = new(tagClass: TagClass.ContextSpecific, tagValue: 1);

        public static bool IsCanonical(ReadOnlyMemory<byte> signatureBytes)
        {
            // ContentInfo
            AsnReader contentInfoReader = new(signatureBytes, AsnEncodingRules.BER);
            AsnReader contentInfo = contentInfoReader.ReadSequence();

            // ContentInfo.contentType
            if (contentInfo.ReadObjectIdentifier() != SignedDataOid)
            {
                throw new UnexpectedCmsContentTypeException();
            }

            // ContentInfo.content and SignedData
            AsnReader signedDataContent = contentInfo.ReadSequence(ContextSpecific0);
            AsnReader signedData = signedDataContent.ReadSequence();

            // SignedData.version, digestAlgorithms, and encapContentInfo
            signedData.ReadInteger();
            signedData.ReadEncodedValue();
            signedData.ReadEncodedValue();

            // SignedData.certificates and crls
            while (signedData.HasData
                && (signedData.PeekTag().HasSameClassAndValue(ContextSpecific0)
                    || signedData.PeekTag().HasSameClassAndValue(ContextSpecific1)))
            {
                signedData.ReadEncodedValue();
            }

            // SignedData.signerInfos and the primary SignerInfo
            AsnReader signerInfos = signedData.ReadSetOf(skipSortOrderValidation: true);
            AsnReader signerInfo = signerInfos.ReadSequence();

            // SignerInfo.version, sid, and digestAlgorithm
            signerInfo.ReadInteger();
            signerInfo.ReadEncodedValue();
            signerInfo.ReadEncodedValue();

            // SignerInfo.signedAttrs is optional.
            if (!signerInfo.HasData
                || !signerInfo.PeekTag().HasSameClassAndValue(ContextSpecific0))
            {
                return true;
            }

            AsnReader signedAttributes = signerInfo.ReadSetOf(
                skipSortOrderValidation: true,
                expectedTag: ContextSpecific0);
            ReadOnlyMemory<byte> previousAttribute = default;

            while (signedAttributes.HasData)
            {
                // Preserve the complete encoded Attribute TLV for the DER SET OF ordering comparison.
                ReadOnlyMemory<byte> attribute = signedAttributes.ReadEncodedValue();

                if (!previousAttribute.IsEmpty
                    && Compare(previousAttribute.Span, attribute.Span) > 0)
                {
                    return false;
                }

                previousAttribute = attribute;
            }

            return true;
        }

        private static int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        {
            int commonLength = Math.Min(left.Length, right.Length);

            for (int index = 0; index < commonLength; index++)
            {
                int comparison = left[index].CompareTo(right[index]);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return left.Length.CompareTo(right.Length);
        }

        internal sealed class UnexpectedCmsContentTypeException : Exception
        {
        }
    }
}
