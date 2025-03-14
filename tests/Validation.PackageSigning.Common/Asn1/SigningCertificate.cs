// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 2634 (https://datatracker.ietf.org/doc/html/rfc2634#section-5.4):

            SigningCertificate ::= SEQUENCE {
                certs        SEQUENCE OF ESSCertID,
                policies     SEQUENCE OF PolicyInformation OPTIONAL
            }
    */
    public sealed class SigningCertificate
    {
        public IReadOnlyList<EssCertId> Certs { get; }
        public IReadOnlyList<PolicyInformation>? Policies { get; }

        private SigningCertificate(IReadOnlyList<EssCertId> certs, IReadOnlyList<PolicyInformation>? policies)
        {
            Certs = certs;
            Policies = policies;
        }

        public static SigningCertificate Create(ReadOnlyMemory<byte> hash, X509Certificate2 certificate)
        {
            if (certificate is null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            EssCertId essCertId = EssCertId.Create(hash, certificate);

            return new SigningCertificate(new[] { essCertId }, policies: null);
        }

        public static SigningCertificate Create(
            IReadOnlyList<EssCertId> essCertIds,
            PolicyInformation? policy = null)
        {
            if (essCertIds is null)
            {
                throw new ArgumentNullException(nameof(essCertIds));
            }

            List<PolicyInformation>? policies = null;

            if (policy is not null)
            {
                policies = new List<PolicyInformation>() { policy };
            }

            return new SigningCertificate(essCertIds, policies);
        }

        public static SigningCertificate Decode(AsnReader reader)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            AsnReader sequenceReader = reader.ReadSequence();
            List<EssCertId> certs = new();
            List<PolicyInformation>? policies = null;

            AsnReader certsSequenceReader = sequenceReader.ReadSequence();

            while (certsSequenceReader.HasData)
            {
                EssCertId essCertId = EssCertId.Decode(certsSequenceReader);

                certs.Add(essCertId);
            }

            if (sequenceReader.HasData)
            {
                policies = new List<PolicyInformation>();
                AsnReader policiesSequenceReader = sequenceReader.ReadSequence();

                while (policiesSequenceReader.HasData)
                {
                    PolicyInformation policy = PolicyInformation.Decode(policiesSequenceReader);

                    policies.Add(policy);
                }
            }

            return new SigningCertificate(certs, policies);
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
                    foreach (EssCertId cert in Certs)
                    {
                        cert.Encode(writer);
                    }
                }

                if (Policies is not null)
                {
                    using (writer.PushSequence())
                    {
                        foreach (PolicyInformation policy in Policies)
                        {
                            policy.Encode(writer);
                        }
                    }
                }
            }
        }
    }
}
