// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Formats.Asn1;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 3161 (https://datatracker.ietf.org/doc/html/rfc3161):

           Accuracy ::= SEQUENCE {
                 seconds        INTEGER              OPTIONAL,
                 millis     [0] INTEGER  (1..999)    OPTIONAL,
                 micros     [1] INTEGER  (1..999)    OPTIONAL  }
    */
    public sealed class Accuracy
    {
        public int? Seconds { get; }
        public int? Milliseconds { get; }
        public int? Microseconds { get; }

        public Accuracy(
            int? seconds,
            int? milliseconds,
            int? microseconds)
        {
            Seconds = seconds;
            Milliseconds = milliseconds;
            Microseconds = microseconds;
        }

        public static Accuracy Decode(AsnReader reader)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            AsnReader sequenceReader = reader.ReadSequence();
            int? seconds = null;
            int? milliseconds = null;
            int? microseconds = null;

            TryReadValue(sequenceReader, Asn1Tag.Integer, seconds);
            TryReadValue(sequenceReader, Asn1Tags.ContextSpecific0, milliseconds);
            TryReadValue(sequenceReader, Asn1Tags.ContextSpecific1, microseconds);

            if (sequenceReader.HasData)
            {
                throw new InvalidAsn1Exception();
            }

            return new Accuracy(seconds, milliseconds, microseconds);
        }

        public void Encode(AsnWriter writer)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            using (writer.PushSequence())
            {
                if (Seconds.HasValue)
                {
                    writer.WriteInteger(Seconds.Value);
                }

                if (Milliseconds.HasValue)
                {
                    writer.WriteInteger(Milliseconds.Value, Asn1Tags.ContextSpecific0);
                }

                if (Microseconds.HasValue)
                {
                    writer.WriteInteger(Microseconds.Value, Asn1Tags.ContextSpecific1);
                }
            }
        }

        private static void TryReadValue(AsnReader reader, Asn1Tag tag, int? value)
        {
            if (reader.HasData && reader.PeekTag().HasSameClassAndValue(tag))
            {
                if (reader.TryReadInt32(out int tmpValue, tag))
                {
                    value = tmpValue;
                }
                else
                {
                    reader.ThrowIfNotEmpty();
                }
            }
        }

        public long? GetTotalMicroseconds()
        {
            return Seconds.GetValueOrDefault() * 1_000_000L +
                Milliseconds.GetValueOrDefault() * 1_000L +
                Microseconds.GetValueOrDefault();
        }
    }
}
