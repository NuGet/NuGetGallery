// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 5035 (https://datatracker.ietf.org/doc/html/rfc5035#section-3):

            SigningCertificateV2 ::= SEQUENCE {
                certs        SEQUENCE OF ESSCertIDv2,
                policies     SEQUENCE OF PolicyInformation OPTIONAL
            }
    */
    public sealed class SigningCertificateV2
    {
        public IReadOnlyList<EssCertIdV2> Certs { get; }
        public IReadOnlyList<PolicyInformation>? Policies { get; }

        public SigningCertificateV2(
            IReadOnlyList<EssCertIdV2> certs,
            IReadOnlyList<PolicyInformation>? policies = null)
        {
            if (certs is null)
            {
                throw new ArgumentNullException(nameof(certs));
            }

            Certs = certs;
            Policies = policies;
        }

        internal static SigningCertificateV2 Decode(AsnReader reader)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            List<EssCertIdV2> certs = new();
            List<PolicyInformation>? policies = null;

            AsnReader sequenceReader = reader.ReadSequence();
            AsnReader certsSequenceReader = sequenceReader.ReadSequence();

            while (certsSequenceReader.HasData)
            {
                EssCertIdV2 cert = EssCertIdV2.Decode(certsSequenceReader);

                certs.Add(cert);
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

            return new SigningCertificateV2(certs, policies);
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
                    foreach (EssCertIdV2 essCertIdV2 in Certs)
                    {
                        essCertIdV2.Encode(writer);
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

        public static SigningCertificateV2 Create(HashAlgorithmName hashAlgorithmName, X509Certificate2 certificate)
        {
            if (certificate is null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            EssCertIdV2 essCertIdV2 = EssCertIdV2.Create(hashAlgorithmName, certificate);

            return new SigningCertificateV2(new[] { essCertIdV2 });
        }
    }
}
