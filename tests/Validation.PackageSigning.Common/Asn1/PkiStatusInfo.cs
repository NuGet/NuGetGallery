// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Formats.Asn1;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 3161 (https://datatracker.ietf.org/doc/html/rfc3161#section-2.4.2):

           PKIStatusInfo ::= SEQUENCE {
              status        PKIStatus,
              statusString  PKIFreeText     OPTIONAL,
              failInfo      PKIFailureInfo  OPTIONAL  }
    */
    internal sealed class PkiStatusInfo
    {
        internal PkiStatus Status { get; }
        internal string? StatusString { get; }
        internal PkiFailureInfo? FailInfo { get; }

        internal PkiStatusInfo(PkiStatus status)
        {
            Status = status;
        }

        internal PkiStatusInfo(PkiStatus status, string? statusString, PkiFailureInfo? failInfo)
            : this(status)
        {
            StatusString = statusString;
            FailInfo = failInfo;
        }

        internal void Encode(AsnWriter writer)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            using (writer.PushSequence())
            {
                writer.WriteInteger((int)Status);

                if (!string.IsNullOrEmpty(StatusString))
                {
                    writer.WriteCharacterString(UniversalTagNumber.UTF8String, StatusString!);
                }

                if (FailInfo is not null)
                {
                    writer.WriteBitString(BitConverter.GetBytes((int)FailInfo));
                }
            }
        }
    }
}
