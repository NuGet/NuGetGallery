// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 6960 (https://datatracker.ietf.org/doc/html/rfc6960#section-4.2.1):

           ResponderID ::= CHOICE {
              byName               [1] Name,
              byKey                [2] KeyHash }

           KeyHash ::= OCTET STRING -- SHA-1 hash of responder's public key
           (excluding the tag and length fields)
    */
    internal sealed class ResponderId
    {
        private readonly X500DistinguishedName? _name;
        private readonly ReadOnlyMemory<byte>? _keyHash = null;

        internal ResponderId(X500DistinguishedName name)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            _name = name;
        }

        internal void Encode(AsnWriter writer)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (_name is not null)
            {
                using (writer.PushSequence(Asn1Tags.ContextSpecific1))
                {
                    writer.WriteEncodedValue(_name.RawData);
                }
            }
            else if (_keyHash is not null)
            {
                throw new NotImplementedException("SHA-1 is not supported.");
            }
            else
            {
                throw new InvalidAsn1Exception();
            }
        }
    }
}
