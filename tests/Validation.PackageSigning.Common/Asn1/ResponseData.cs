// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Numerics;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 6960 (https://www.rfc-editor.org/rfc/rfc6960#section-4.2.1):

           ResponseData ::= SEQUENCE {
              version              [0] EXPLICIT Version DEFAULT v1,
              responderID              ResponderID,
              producedAt               GeneralizedTime,
              responses                SEQUENCE OF SingleResponse,
              responseExtensions   [1] EXPLICIT Extensions OPTIONAL }
    */
    internal sealed class ResponseData
    {
        internal BigInteger Version { get; }
        internal ResponderId ResponderId { get; }
        internal DateTimeOffset ProducedAt { get; }
        internal IReadOnlyList<SingleResponse> Responses { get; }
        internal IReadOnlyList<X509ExtensionAsn>? ResponseExtensions { get; }

        internal ResponseData(
            BigInteger version,
            ResponderId responderId,
            DateTimeOffset producedAt,
            IReadOnlyList<SingleResponse> responses,
            IReadOnlyList<X509ExtensionAsn>? responseExtensions)
        {
            Version = version;
            ResponderId = responderId;
            ProducedAt = producedAt;
            Responses = responses;
            ResponseExtensions = responseExtensions;
        }

        internal void Encode(AsnWriter writer)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            using (writer.PushSequence())
            {
                //using (writer.PushSequence(Asn1Tags.ContextSpecific0))
                //{
                //    writer.WriteInteger(Version);
                //}

                ResponderId.Encode(writer);
                writer.WriteGeneralizedTime(ProducedAt, omitFractionalSeconds: true);

                using (writer.PushSequence())
                {
                    foreach (SingleResponse response in Responses)
                    {
                        response.Encode(writer);
                    }
                }

                if (ResponseExtensions?.Count > 0)
                {
                    using (writer.PushSequence(Asn1Tags.ContextSpecific1))
                    using (writer.PushSequence())
                    {
                        foreach (X509ExtensionAsn extension in ResponseExtensions)
                        {
                            extension.Encode(writer);
                        }
                    }
                }
            }
        }
    }
}
