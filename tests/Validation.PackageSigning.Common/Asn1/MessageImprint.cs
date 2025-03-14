// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Formats.Asn1;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 3161 (https://tools.ietf.org/html/rfc3161#section-2.4.2):

            MessageImprint ::= SEQUENCE  {
                hashAlgorithm                AlgorithmIdentifier,
                hashedMessage                OCTET STRING  }
    */
    public sealed class MessageImprint
    {
        public AlgorithmIdentifier HashAlgorithm { get; }
        public ReadOnlyMemory<byte> HashedMessage { get; }

        public MessageImprint(
            AlgorithmIdentifier hashAlgorithm,
            ReadOnlyMemory<byte> hashedMessage)
        {
            if (hashAlgorithm is null)
            {
                throw new ArgumentNullException(nameof(hashAlgorithm));
            }

            HashAlgorithm = hashAlgorithm;
            HashedMessage = hashedMessage;
        }

        public static MessageImprint Decode(AsnReader reader)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            AsnReader sequenceReader = reader.ReadSequence();

            AlgorithmIdentifier algorithmIdentifier = AlgorithmIdentifier.Decode(sequenceReader);
            ReadOnlyMemory<byte> hash = sequenceReader.ReadOctetString();

            sequenceReader.ThrowIfNotEmpty();

            return new MessageImprint(algorithmIdentifier, hash);
        }

        public void Encode(AsnWriter writer)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            using (writer.PushSequence())
            {
                HashAlgorithm.Encode(writer);
                writer.WriteOctetString(HashedMessage.Span);
            }
        }
    }
}
