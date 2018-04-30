// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace NuGetGallery
{
    public sealed class CertificateValidator : ICertificateValidator
    {
        private const int MaximumSizeInBytes = 10000;

        public void Validate(HttpPostedFileBase file)
        {
            if (file == null)
            {
                throw new UserSafeException(Strings.CertificateFileIsRequired, new ArgumentNullException(nameof(file)));
            }

            if (!string.Equals(
                Path.GetExtension(file.FileName),
                CoreConstants.CertificateFileExtension,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new UserSafeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        Strings.ValidateCertificate_InvalidFileType,
                        CoreConstants.CertificateFileExtension));
            }

            var stream = file.InputStream;

            if (stream == null)
            {
                throw new UserSafeException(Strings.ValidateCertificate_InvalidStream);
            }

            if (!stream.CanSeek)
            {
                throw new UserSafeException(Strings.ValidateCertificate_StreamMustBeSeekable);
            }

            if (file.ContentLength <= 0 || stream.Length <= 0)
            {
                throw new UserSafeException(Strings.ValidateCertificate_InvalidFileLength);
            }

            if (file.ContentLength > MaximumSizeInBytes || stream.Length > MaximumSizeInBytes)
            {
                throw new UserSafeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        Strings.ValidateCertificate_FileTooLarge,
                        MaximumSizeInBytes));
            }

            if (!IsDerEncodedX509Certificate(stream))
            {
                throw new UserSafeException(Strings.ValidateCertificate_InvalidEncoding);
            }
        }

        private static bool IsDerEncodedX509Certificate(Stream stream)
        {
            stream.Position = 0;

            try
            {
                using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
                {
                    var firstByte = reader.ReadByte();

                    const byte ConstructedSequence = 0x30;

                    // A DER encoded binary X.509 certificate begins with a constructed sequence tag.
                    if (firstByte != ConstructedSequence)
                    {
                        return false;
                    }

                    // However, so do many other DER encoded files (e.g.:  PFX, P7B, P7S, etc.).
                    // We will have reasonable confidence that this file is a certificate file and not one
                    // of these other types by inspecting the next ASN.1 tag.  But first we need to read the
                    // length for the current sequence.

                    var value = reader.ReadByte();
                    int length;

                    if ((value & 0x80) == 0x80)
                    {
                        // Length is in long form.
                        // The length is represented by a byte sequence of length byteCount.
                        var byteCount = value & 0x7F;

                        // The previously checked constructed sequence tag and initial length byte
                        // subtract from the overall length.
                        var maxByteCount = GetLengthByteCount(MaximumSizeInBytes - 2);

                        if (byteCount > maxByteCount)
                        {
                            // The sequence is larger than the maximum file size allows,
                            // so don't even try calculating the actual length.
                            return false;
                        }

                        var lengthBytes = reader.ReadBytes(byteCount);

                        length = lengthBytes.Aggregate(
                            seed: 0,
                            func: (l, r) => (l * 256) + r);
                    }
                    else
                    {
                        // Length is in short form.
                        length = value;
                    }

                    var remainingBytes = stream.Length - stream.Position;

                    if (length != remainingBytes)
                    {
                        return false;
                    }

                    if (stream.Position == stream.Length)
                    {
                        return false;
                    }

                    // A X.509 certificate (https://tools.ietf.org/html/rfc5280#section-4.1) is a SEQUENCE
                    // with a SEQUENCE as its first nested type.
                    //
                    // In contrast, a PKCS #12 file (https://tools.ietf.org/html/rfc7292#section-4) is a SEQUENCE
                    // with an INTEGER as its first nested type.
                    return reader.ReadByte() == ConstructedSequence;
                }
            }
            finally
            {
                stream.Position = 0;
            }
        }

        private static int GetLengthByteCount(uint value)
        {
            var digits = 1;

            while (value > 255)
            {
                var digit = value % 256;

                value /= 256;

                ++digits;
            }

            return digits;
        }
    }
}