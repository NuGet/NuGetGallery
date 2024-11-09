// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 2634 (https://tools.ietf.org/html/rfc2634#section-5.4.1):

            IssuerSerial ::= SEQUENCE {
                issuer                   GeneralNames,
                serialNumber             CertificateSerialNumber
            }

        From RFC 2634 (https://tools.ietf.org/html/rfc3280#section-4.2.1.7):

            GeneralNames ::= SEQUENCE SIZE (1..MAX) OF GeneralName

        From RFC 3280 (https://tools.ietf.org/html/rfc3280#section-4.1):

            CertificateSerialNumber  ::=  INTEGER
    */
    public sealed class IssuerSerial
    {
        public IReadOnlyList<GeneralName> GeneralNames { get; }
        public BigInteger SerialNumber { get; }

        public IssuerSerial(IReadOnlyList<GeneralName> generalNames, BigInteger serialNumber)
        {
            GeneralNames = generalNames;
            SerialNumber = serialNumber;
        }

        public static IssuerSerial Decode(AsnReader reader)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            AsnReader sequenceReader = reader.ReadSequence();
            AsnReader generalNamesSequenceReader = reader.ReadSequence();
            List<GeneralName> generalNames = new();

            while (generalNamesSequenceReader.HasData)
            {
                GeneralName generalName = GeneralName.Decode(generalNamesSequenceReader);

                generalNames.Add(generalName);
            }

            if (generalNames.Count == 0)
            {
                throw new InvalidAsn1Exception();
            }

            BigInteger serialNumber = sequenceReader.ReadInteger();

            sequenceReader.ThrowIfNotEmpty();

            return new IssuerSerial(generalNames, serialNumber);
        }

        public void Encode(AsnWriter writer)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            using (writer.PushSequence())
            {
                using (writer.PushSequence())
                {
                    foreach (GeneralName generalName in GeneralNames)
                    {
                        generalName.Encode(writer);
                    }
                }

                writer.WriteInteger(SerialNumber);
            }
        }

        internal static IssuerSerial Create(X509Certificate2 certificate)
        {
            List<GeneralName> generalNames = new()
            {
                new GeneralName(directoryName: certificate.IssuerName.RawData)
            };

            BigInteger serialNumber = new(certificate.GetSerialNumber());

            return new IssuerSerial(generalNames, serialNumber);
        }
    }
}
