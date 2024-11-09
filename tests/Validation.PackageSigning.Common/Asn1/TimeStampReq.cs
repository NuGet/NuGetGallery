// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 3161 (https://datatracker.ietf.org/doc/html/rfc3161#section-2.4.1):

            TimeStampReq ::= SEQUENCE  {
               version                      INTEGER  { v1(1) },
               messageImprint               MessageImprint,
                 --a hash algorithm OID and the hash value of the data to be
                 --time-stamped
               reqPolicy             TSAPolicyId              OPTIONAL,
               nonce                 INTEGER                  OPTIONAL,
               certReq               BOOLEAN                  DEFAULT FALSE,
               extensions            [0] IMPLICIT Extensions  OPTIONAL  }

            MessageImprint ::= SEQUENCE  {
                hashAlgorithm                AlgorithmIdentifier,
                hashedMessage                OCTET STRING  }

            TSAPolicyId ::= OBJECT IDENTIFIER
    */
    internal sealed class TimeStampReq
    {
        internal BigInteger Version { get; }
        internal MessageImprint MessageImprint { get; }
        internal BigInteger? Nonce { get; }
        internal bool CertReq { get; }

        private TimeStampReq(
            BigInteger version,
            MessageImprint messageImprint,
            BigInteger? nonce,
            bool certReq)
        {
            Version = version;
            MessageImprint = messageImprint;
            Nonce = nonce;
            CertReq = certReq;
        }

        internal static TimeStampReq Decode(ReadOnlyMemory<byte> bytes)
        {
            AsnReader reader = new(bytes, AsnEncodingRules.DER);
            AsnReader sequenceReader = reader.ReadSequence();

            BigInteger version = sequenceReader.ReadInteger();
            MessageImprint messageImprint = MessageImprint.Decode(sequenceReader);
            Oid? reqPolicy = null;

            if (sequenceReader.HasData && sequenceReader.PeekTag().TagValue == (int)UniversalTagNumber.ObjectIdentifier)
            {
                reqPolicy = new Oid(sequenceReader.ReadObjectIdentifier());
            }

            BigInteger? nonce = null;

            if (sequenceReader.HasData && sequenceReader.PeekTag().TagValue == (int)UniversalTagNumber.Integer)
            {
                nonce = sequenceReader.ReadInteger();
            }

            bool certReq = false;

            if (sequenceReader.HasData && sequenceReader.PeekTag().TagValue == (int)UniversalTagNumber.Boolean)
            {
                certReq = sequenceReader.ReadBoolean();
            }

            // ignore extensions

            return new TimeStampReq(version, messageImprint, nonce, certReq);
        }
    }
}
