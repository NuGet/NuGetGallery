// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 3161 (https://datatracker.ietf.org/doc/html/rfc3161#section-2.4.2):

            TSTInfo ::= SEQUENCE  {
               version                      INTEGER  { v1(1) },
               policy                       TSAPolicyId,
               messageImprint               MessageImprint,
                 -- MUST have the same value as the similar field in
                 -- TimeStampReq
               serialNumber                 INTEGER,
                -- Time-Stamping users MUST be ready to accommodate integers
                -- up to 160 bits.
               genTime                      GeneralizedTime,
               accuracy                     Accuracy                 OPTIONAL,
               ordering                     BOOLEAN             DEFAULT FALSE,
               nonce                        INTEGER                  OPTIONAL,
                 -- MUST be present if the similar field was present
                 -- in TimeStampReq.  In that case it MUST have the same value.
               tsa                          [0] GeneralName          OPTIONAL,
               extensions                   [1] IMPLICIT Extensions   OPTIONAL  }
    */
    public sealed class TstInfo
    {
        public BigInteger Version { get; }
        public Oid Policy { get; }
        public MessageImprint MessageImprint { get; }
        public BigInteger SerialNumber { get; }
        public DateTimeOffset Timestamp { get; }
        public Accuracy? Accuracy { get; }
        public bool Ordering { get; }
        public BigInteger? Nonce { get; }
        public GeneralName? Tsa { get; }
        public X509ExtensionCollection? Extensions { get; }

        public TstInfo(
            BigInteger version,
            Oid policy,
            MessageImprint messageImprint,
            BigInteger serialNumber,
            DateTimeOffset timestamp,
            Accuracy? accuracy = default,
            bool ordering = false,
            BigInteger? nonce = default,
            GeneralName? tsa = null,
            X509ExtensionCollection? extensions = null)
        {
            if (policy is null)
            {
                throw new ArgumentNullException(nameof(policy));
            }

            if (messageImprint is null)
            {
                throw new ArgumentNullException(nameof(messageImprint));
            }

            Version = version;
            Policy = policy;
            MessageImprint = messageImprint;
            SerialNumber = serialNumber;
            Timestamp = timestamp;
            Accuracy = accuracy;
            Nonce = nonce;
            Tsa = tsa;
            Extensions = extensions;
        }

        public byte[] Encode(AsnWriter writer, bool omitFractionalSeconds = false)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            using (writer.PushSequence())
            {
                writer.WriteInteger(Version);
                writer.WriteObjectIdentifier(Policy.Value!);
                MessageImprint.Encode(writer);
                writer.WriteInteger(SerialNumber);
                writer.WriteGeneralizedTime(Timestamp, omitFractionalSeconds);

                if (Accuracy is not null)
                {
                    Accuracy.Encode(writer);
                }

                writer.WriteBoolean(Ordering);

                if (Nonce is not null)
                {
                    writer.WriteInteger(Nonce.Value);
                }

                if (Tsa is not null)
                {
                    using (writer.PushSequence(Asn1Tags.ContextSpecific0))
                    {
                        Tsa.Encode(writer);
                    }
                }

                if (Extensions is not null)
                {
                    using (writer.PushSequence(Asn1Tags.ContextSpecific1))
                    {
                        foreach (X509Extension extension in Extensions)
                        {
                            using (writer.PushSequence())
                            {
                                writer.WriteObjectIdentifier(extension.Oid!.Value!);
                                writer.WriteBoolean(extension.Critical);
                                writer.WriteOctetString(extension.RawData);
                            }
                        }
                    }
                }
            }

            return writer.Encode();
        }
    }
}
