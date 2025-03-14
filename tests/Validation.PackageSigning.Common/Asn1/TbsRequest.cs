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
        From RFC 6960 (https://datatracker.ietf.org/doc/html/rfc6960#section-4.1):

           TBSRequest      ::=     SEQUENCE {
               version             [0]     EXPLICIT Version DEFAULT v1,
               requestorName       [1]     EXPLICIT GeneralName OPTIONAL,
               requestList                 SEQUENCE OF Request,
               requestExtensions   [2]     EXPLICIT Extensions OPTIONAL }
    */
    internal sealed class TbsRequest
    {
        internal BigInteger Version { get; }
        internal GeneralName? RequestorName { get; }
        internal IReadOnlyList<Request> RequestList { get; }
        internal IReadOnlyList<X509ExtensionAsn>? RequestExtensions { get; }

        private TbsRequest(
            BigInteger version,
            GeneralName? requestorName,
            IReadOnlyList<Request> requestList,
            IReadOnlyList<X509ExtensionAsn>? requestExtensions)
        {
            if (requestList is null)
            {
                throw new ArgumentNullException(nameof(requestList));
            }

            Version = version;
            RequestorName = requestorName;
            RequestList = requestList;
            RequestExtensions = requestExtensions;
        }

        internal static TbsRequest Decode(AsnReader reader)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            AsnReader sequenceReader = reader.ReadSequence();
            Asn1Tag tag = sequenceReader.PeekTag();
            BigInteger version = 1;

            if (tag.HasSameClassAndValue(Asn1Tags.ContextSpecific0))
            {
                AsnReader explicitReader = sequenceReader.ReadSequence(Asn1Tags.ContextSpecific0);
                version = explicitReader.ReadInteger();

                tag = sequenceReader.PeekTag();
            }

            GeneralName? requestorName = null;

            if (tag.HasSameClassAndValue(Asn1Tags.ContextSpecific1))
            {
                AsnReader explicitReader = sequenceReader.ReadSequence(Asn1Tags.ContextSpecific1);
                requestorName = GeneralName.Decode(explicitReader);
                tag = sequenceReader.PeekTag();
            }

            AsnReader requestListReader = sequenceReader.ReadSequence();
            List<Request> requests = new();

            while (requestListReader.HasData)
            {
                Request request = Request.Decode(requestListReader);

                requests.Add(request);
            }

            List<X509ExtensionAsn> requestExtensions = new();

            if (sequenceReader.HasData)
            {
                tag = sequenceReader.PeekTag();

                Asn1Tag requestExtensionsTag = new(TagClass.ContextSpecific, tagValue: 2);

                if (!tag.HasSameClassAndValue(requestExtensionsTag))
                {
                    throw new InvalidAsn1Exception();
                }

                AsnReader explicitReader = sequenceReader.ReadSequence(requestExtensionsTag);
                AsnReader requestExtensionsReader = explicitReader.ReadSequence();

                while (requestExtensionsReader.HasData)
                {
                    X509ExtensionAsn.Decode(
                        ref requestExtensionsReader,
                        rebind: default,
                        out X509ExtensionAsn extensionsDecoded);

                    requestExtensions.Add(extensionsDecoded);
                }
            }

            return new TbsRequest(
                version,
                requestorName,
                requests,
                requestExtensions);
        }
    }
}
