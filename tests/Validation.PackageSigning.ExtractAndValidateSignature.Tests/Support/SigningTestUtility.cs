// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;

namespace Validation.PackageSigning.ExtractAndValidateSignature.Tests
{
    /// <summary>
    /// Source:
    /// https://github.com/NuGet/NuGet.Client/blob/7a8fe0bd7397e28a4fda6479b49e22df76578ece/test/TestUtilities/Test.Utility/Signing/SigningTestUtility.cs
    /// </summary>
    public static class SigningTestUtility
    {
        public static X509Certificate2 GenerateCertificate(
            string subjectName,
            Action<X509V3CertificateGenerator> modifyGenerator,
            string signatureAlgorithm = "SHA256WITHRSA",
            int publicKeyLength = 2048,
            ChainCertificateRequest chainCertificateRequest = null)
        {
            if (string.IsNullOrEmpty(subjectName))
            {
                subjectName = "NuGetTest";
            }

            var random = new SecureRandom();
            var pairGenerator = new RsaKeyPairGenerator();
            var genParams = new KeyGenerationParameters(random, publicKeyLength);
            pairGenerator.Init(genParams);
            var issuerKeyPair = pairGenerator.GenerateKeyPair();

            // Create cert
            var subjectDN = $"CN={subjectName}";
            var certGen = new X509V3CertificateGenerator();
            certGen.SetSubjectDN(new X509Name(subjectDN));

            // default to new key pair
            var issuerPrivateKey = issuerKeyPair.Private;
            var keyUsage = KeyUsage.DigitalSignature;
            var issuerDN = chainCertificateRequest?.IssuerDN ?? subjectDN;
            certGen.SetIssuerDN(new X509Name(issuerDN));
            
            if (chainCertificateRequest != null)
            {
                if (chainCertificateRequest.Issuer != null)
                {
                    // for a certificate with an issuer assign Authority Key Identifier
                    var issuer = chainCertificateRequest?.Issuer;
                    var bcIssuer = DotNetUtilities.FromX509Certificate(issuer);
                    var authorityKeyIdentifier = new AuthorityKeyIdentifierStructure(bcIssuer);
                    issuerPrivateKey = DotNetUtilities.GetKeyPair(issuer.PrivateKey).Private;
                    certGen.AddExtension(X509Extensions.AuthorityKeyIdentifier.Id, false, authorityKeyIdentifier);
                }

                if (chainCertificateRequest.ConfigureCrl)
                {
                    // for a certificate in a chain create CRL distribution point extension
                    var crlServerUri = $"{chainCertificateRequest.CrlServerBaseUri}{issuerDN}.crl";
                    var generalName = new GeneralName(GeneralName.UniformResourceIdentifier, new DerIA5String(crlServerUri));
                    var distPointName = new DistributionPointName(new GeneralNames(generalName));
                    var distPoint = new DistributionPoint(distPointName, null, null);

                    certGen.AddExtension(X509Extensions.CrlDistributionPoints, critical: false, extensionValue: new DerSequence(distPoint));
                }

                if (chainCertificateRequest.IsCA)
                {
                    // update key usage with CA cert sign and crl sign attributes
                    keyUsage |= KeyUsage.CrlSign | KeyUsage.KeyCertSign;
                }
            }

            certGen.SetNotAfter(DateTime.UtcNow.Add(TimeSpan.FromHours(1)));
            certGen.SetNotBefore(DateTime.UtcNow.Subtract(TimeSpan.FromHours(1)));
            certGen.SetPublicKey(issuerKeyPair.Public);

            var serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(long.MaxValue), random);
            certGen.SetSerialNumber(serialNumber);

            var subjectKeyIdentifier = new SubjectKeyIdentifier(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(issuerKeyPair.Public));
            certGen.AddExtension(X509Extensions.SubjectKeyIdentifier.Id, false, subjectKeyIdentifier);

            certGen.AddExtension(X509Extensions.KeyUsage.Id, false, new KeyUsage(keyUsage));
            certGen.AddExtension(X509Extensions.BasicConstraints.Id, true, new BasicConstraints(chainCertificateRequest?.IsCA ?? false));

            // Allow changes
            modifyGenerator?.Invoke(certGen);

            var signatureFactory = new Asn1SignatureFactory(signatureAlgorithm, issuerPrivateKey, random);
            var certificate = certGen.Generate(signatureFactory);
            var certResult = new X509Certificate2(certificate.GetEncoded());
            
            certResult.PrivateKey = DotNetUtilities.ToRSA(issuerKeyPair.Private as RsaPrivateCrtKeyParameters);

            return certResult;
        }
    }
}
