// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Security.Cryptography;
using NuGet.Packaging.Signing;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 5280 (https://datatracker.ietf.org/doc/html/rfc5280#appendix-A.2):

            PolicyInformation ::= SEQUENCE {
                policyIdentifier   CertPolicyId,
                policyQualifiers   SEQUENCE SIZE (1..MAX) OF
                                        PolicyQualifierInfo OPTIONAL }

            CertPolicyId ::= OBJECT IDENTIFIER
    */
    public sealed class PolicyInformation
    {
        public Oid PolicyIdentifier { get; }
        public IReadOnlyList<PolicyQualifierInfo>? PolicyQualifiers { get; }

        public PolicyInformation(Oid policyIdentifier, IReadOnlyList<PolicyQualifierInfo>? policyQualifiers = null)
        {
            PolicyIdentifier = policyIdentifier;
            PolicyQualifiers = policyQualifiers;
        }

        public static PolicyInformation Decode(AsnReader reader)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            AsnReader sequenceReader = reader.ReadSequence();
            Oid policyIdentifier = new(sequenceReader.ReadObjectIdentifier());
            List<PolicyQualifierInfo>? policyQualifiers = null;

            if (sequenceReader.HasData)
            {
                policyQualifiers = new List<PolicyQualifierInfo>();
                AsnReader policyQualifiersSequenceReader = sequenceReader.ReadSequence();
                bool isAnyPolicy = policyIdentifier.Value == Oids.AnyPolicy;

                while (policyQualifiersSequenceReader.HasData)
                {
                    PolicyQualifierInfo policyQualifier = PolicyQualifierInfo.Decode(policyQualifiersSequenceReader);

                    if (isAnyPolicy)
                    {
                        if (policyQualifier.PolicyQualifierId.Value != Oids.IdQtCps &&
                            policyQualifier.PolicyQualifierId.Value != Oids.IdQtUnotice)
                        {
                            throw new InvalidAsn1Exception();
                        }
                    }

                    policyQualifiers.Add(policyQualifier);
                }

                if (policyQualifiers.Count == 0)
                {
                    throw new InvalidAsn1Exception();
                }
            }

            sequenceReader.ThrowIfNotEmpty();

            return new PolicyInformation(policyIdentifier, policyQualifiers);
        }

        public void Encode(AsnWriter writer)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(PolicyIdentifier.Value!);

                if (PolicyQualifiers is not null && PolicyQualifiers.Count > 0)
                {
                    using (writer.PushSequence())
                    {
                        foreach (PolicyQualifierInfo policyQualifier in PolicyQualifiers)
                        {
                            policyQualifier.Encode(writer);
                        }
                    }
                }
            }
        }
    }
}
