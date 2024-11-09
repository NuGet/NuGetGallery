// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Formats.Asn1;
using System.Security.Cryptography;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 3280 (https://datatracker.ietf.org/doc/html/rfc3280#section-4.1.1.2):

            AlgorithmIdentifier ::= SEQUENCE {
                algorithm               OBJECT IDENTIFIER,
                parameters              ANY DEFINED BY algorithm OPTIONAL
            }
    */
    public sealed class AlgorithmIdentifier
    {
        public Oid Algorithm { get; }
        public ReadOnlyMemory<byte>? Parameters { get; }

        public AlgorithmIdentifier(Oid algorithm, ReadOnlyMemory<byte>? parameters = null)
        {
            Algorithm = algorithm;
            Parameters = parameters;
        }

        public static AlgorithmIdentifier Decode(AsnReader reader)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            AsnReader sequenceReader = reader.ReadSequence();

            Oid algorithm = new(sequenceReader.ReadObjectIdentifier());
            ReadOnlyMemory<byte>? parameters = null;

            if (sequenceReader.HasData)
            {
                parameters = sequenceReader.ReadEncodedValue();
            }

            sequenceReader.ThrowIfNotEmpty();

            return new AlgorithmIdentifier(algorithm, parameters);
        }

        public void Encode(AsnWriter writer)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(Algorithm.Value!);

                if (Parameters.HasValue)
                {
                    writer.WriteEncodedValue(Parameters.Value.Span);
                }
            }
        }
    }
}
