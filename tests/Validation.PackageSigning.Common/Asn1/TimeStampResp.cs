// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Formats.Asn1;
using System.Security.Cryptography.Pkcs;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 3161 (https://datatracker.ietf.org/doc/html/rfc3161#section-2.4.2):

           TimeStampResp ::= SEQUENCE  {
              status                  PKIStatusInfo,
              timeStampToken          TimeStampToken     OPTIONAL  }

           TimeStampToken ::= ContentInfo
             -- contentType is id-signedData ([CMS])
             -- content is SignedData ([CMS])
    */
    internal sealed class TimeStampResp
    {
        private readonly PkiStatusInfo _status;
        private readonly SignedCms? _timeStampToken;

        internal TimeStampResp(PkiStatusInfo status, SignedCms? timeStampToken = null)
        {
            _status = status;
            _timeStampToken = timeStampToken;
        }

        internal ReadOnlyMemory<byte> Encode()
        {
            AsnWriter writer = new(AsnEncodingRules.DER);

            using (writer.PushSequence())
            {
                _status.Encode(writer);

                if (_timeStampToken is not null)
                {
                    writer.WriteEncodedValue(_timeStampToken.Encode());
                }
            }

            return writer.Encode();
        }
    }
}
