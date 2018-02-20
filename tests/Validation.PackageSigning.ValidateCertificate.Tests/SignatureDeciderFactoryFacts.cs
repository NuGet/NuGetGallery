// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using NuGet.Services.Validation;
using Xunit;

namespace Validation.PackageSigning.ValidateCertificate.Tests
{
    public class SignatureDeciderFactoryFacts
    {
        public class TheMakeDeciderForRevokedCertificateMethod : FactsBase
        {
            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void RevokedTimestampingCertificateInvalidatesSignatures(bool nullRevocationTime)
            {
                // Arrange - only signatures at ingestion should be rejected, all other signatures should
                // raise a warning. The revocation time should not matter.
                var revocationTime = nullRevocationTime ? (DateTime?)null : DateTime.Now;
                var certificate = new EndCertificate { Use = EndCertificateUse.Timestamping };
                var result = new CertificateVerificationResult(
                                    status: EndCertificateStatus.Revoked,
                                    statusFlags: X509ChainStatusFlags.Revoked,
                                    revocationTime: revocationTime);

                var signatureAtIngestion = new PackageSignature { Status = PackageSignatureStatus.Unknown };
                var signatureAtGracePeriod = new PackageSignature { Status = PackageSignatureStatus.InGracePeriod };
                var signatureAfterGracePeriod = new PackageSignature { Status = PackageSignatureStatus.Valid };

                // Act & Assert
                var decider = _target.MakeDeciderForRevokedCertificate(certificate, result);

                Assert.Equal(SignatureDecision.Reject, decider(signatureAtIngestion));
                Assert.Equal(SignatureDecision.Warn, decider(signatureAtGracePeriod));
                Assert.Equal(SignatureDecision.Warn, decider(signatureAfterGracePeriod));
            }

            [Fact]
            public void RevokedCodeSigningCertificateWithoutRevocationDateInvalidatesAllSignatures()
            {
                var certificate = new EndCertificate { Use = EndCertificateUse.CodeSigning };

                var result = new CertificateVerificationResult(
                                    status: EndCertificateStatus.Revoked,
                                    statusFlags: X509ChainStatusFlags.Revoked,
                                    revocationTime: null);

                var signatureAtIngestion = new PackageSignature { Status = PackageSignatureStatus.Unknown };
                var signatureAtGracePeriod = new PackageSignature { Status = PackageSignatureStatus.InGracePeriod };
                var signatureAfterGracePeriod = new PackageSignature { Status = PackageSignatureStatus.Valid };

                // Act & Assert
                var decider = _target.MakeDeciderForRevokedCertificate(certificate, result);

                Assert.Equal(SignatureDecision.Reject, decider(signatureAtIngestion));
                Assert.Equal(SignatureDecision.Reject, decider(signatureAtGracePeriod));
                Assert.Equal(SignatureDecision.Reject, decider(signatureAfterGracePeriod));
            }

            [Fact]
            public void RevokedCodeSigningCertificateThrowsIfThereIsARevocationDateAndNoTrustedTimestamps()
            {
                var certificate = new EndCertificate { Use = EndCertificateUse.CodeSigning };

                var result = new CertificateVerificationResult(
                                    status: EndCertificateStatus.Revoked,
                                    statusFlags: X509ChainStatusFlags.Revoked,
                                    revocationTime: DateTime.UtcNow);

                var signatureAtIngestion = new PackageSignature
                {
                    Status = PackageSignatureStatus.Unknown,
                    TrustedTimestamps = new TrustedTimestamp[0],
                };

                var signatureAtGracePeriod = new PackageSignature
                {
                    Status = PackageSignatureStatus.InGracePeriod,
                    TrustedTimestamps = new TrustedTimestamp[0],
                };

                var signatureAfterGracePeriod = new PackageSignature
                {
                    Status = PackageSignatureStatus.Valid,
                    TrustedTimestamps = new TrustedTimestamp[0],
                };

                // Act & Assert
                var decider = _target.MakeDeciderForRevokedCertificate(certificate, result);

                Assert.Throws<InvalidOperationException>(() => decider(signatureAtIngestion));
                Assert.Throws<InvalidOperationException>(() => decider(signatureAtGracePeriod));
                Assert.Throws<InvalidOperationException>(() => decider(signatureAfterGracePeriod));
            }

            [Fact]
            public void RevokedCodeSigningCertificateThrowsIfThereIsARevocationDateAndMultipleTrustedTimestamps()
            {
                var certificate = new EndCertificate { Use = EndCertificateUse.CodeSigning };

                var result = new CertificateVerificationResult(
                                    status: EndCertificateStatus.Revoked,
                                    statusFlags: X509ChainStatusFlags.Revoked,
                                    revocationTime: DateTime.UtcNow);

                var signatureAtIngestion = new PackageSignature
                {
                    Status = PackageSignatureStatus.Unknown,
                    TrustedTimestamps = new TrustedTimestamp[]
                    {
                        new TrustedTimestamp(),
                        new TrustedTimestamp(),
                    }
                };

                var signatureAtGracePeriod = new PackageSignature
                {
                    Status = PackageSignatureStatus.InGracePeriod,
                    TrustedTimestamps = new TrustedTimestamp[]
                    {
                        new TrustedTimestamp(),
                        new TrustedTimestamp(),
                    }
                };

                var signatureAfterGracePeriod = new PackageSignature
                {
                    Status = PackageSignatureStatus.Valid,
                    TrustedTimestamps = new TrustedTimestamp[]
                    {
                        new TrustedTimestamp(),
                        new TrustedTimestamp(),
                    }
                };

                // Act & Assert
                var decider = _target.MakeDeciderForRevokedCertificate(certificate, result);

                Assert.Throws<InvalidOperationException>(() => decider(signatureAtIngestion));
                Assert.Throws<InvalidOperationException>(() => decider(signatureAtGracePeriod));
                Assert.Throws<InvalidOperationException>(() => decider(signatureAfterGracePeriod));
            }

            [Fact]
            public void RevokedCodeSigningCertificateRejectsAllSignaturesIfTimestampIsAlreadyInvalid()
            {
                var certificate = new EndCertificate { Use = EndCertificateUse.CodeSigning };

                var result = new CertificateVerificationResult(
                                    status: EndCertificateStatus.Revoked,
                                    statusFlags: X509ChainStatusFlags.Revoked,
                                    revocationTime: DateTime.UtcNow);

                var signatureAtIngestion = new PackageSignature
                {
                    Status = PackageSignatureStatus.Unknown,
                    TrustedTimestamps = new TrustedTimestamp[]
                    {
                        new TrustedTimestamp { Status = TrustedTimestampStatus.Invalid }
                    }
                };

                var signatureAtGracePeriod = new PackageSignature
                {
                    Status = PackageSignatureStatus.InGracePeriod,
                    TrustedTimestamps = new TrustedTimestamp[]
                    {
                        new TrustedTimestamp { Status = TrustedTimestampStatus.Invalid }
                    }
                };

                var signatureAfterGracePeriod = new PackageSignature
                {
                    Status = PackageSignatureStatus.Valid,
                    TrustedTimestamps = new TrustedTimestamp[]
                    {
                        new TrustedTimestamp { Status = TrustedTimestampStatus.Invalid }
                    }
                };

                // Act & Assert
                var decider = _target.MakeDeciderForRevokedCertificate(certificate, result);

                Assert.Equal(SignatureDecision.Reject, decider(signatureAtIngestion));
                Assert.Equal(SignatureDecision.Reject, decider(signatureAtGracePeriod));
                Assert.Equal(SignatureDecision.Reject, decider(signatureAfterGracePeriod));
            }

            [Theory]
            [MemberData(nameof(RevokedCodeSigningCertificateWithRevocationDateInvalidatesSignaturesData))]
            public void RevokedCodeSigningCertificateWithRevocationDateInvalidatesSignatures(
                TimeSpan signatureTimeDeltaToRevocationTime,
                SignatureDecision ingestionDecision,
                SignatureDecision gracePeriodDecision,
                SignatureDecision afterGracePeriodDecision)
            {
                // Arrange - only signatures that were created after the revocation date should
                // be rejected.
                var revocationTime = DateTime.UtcNow;
                var certificate = new EndCertificate { Use = EndCertificateUse.CodeSigning };
                var timestamp = new TrustedTimestamp { Value = revocationTime + signatureTimeDeltaToRevocationTime, Status = TrustedTimestampStatus.Valid };

                var result = new CertificateVerificationResult(
                                    status: EndCertificateStatus.Revoked,
                                    statusFlags: X509ChainStatusFlags.Revoked,
                                    revocationTime: revocationTime);

                var signatureAtIngestion = new PackageSignature { Status = PackageSignatureStatus.Unknown };
                var signatureAtGracePeriod = new PackageSignature { Status = PackageSignatureStatus.InGracePeriod };
                var signatureAfterGracePeriod = new PackageSignature { Status = PackageSignatureStatus.Valid };

                signatureAtIngestion.TrustedTimestamps = new[] { timestamp };
                signatureAtGracePeriod.TrustedTimestamps = new[] { timestamp };
                signatureAfterGracePeriod.TrustedTimestamps = new[] { timestamp };

                // Act & Assert
                var decider = _target.MakeDeciderForRevokedCertificate(certificate, result);

                Assert.Equal(ingestionDecision, decider(signatureAtIngestion));
                Assert.Equal(gracePeriodDecision, decider(signatureAtGracePeriod));
                Assert.Equal(afterGracePeriodDecision, decider(signatureAfterGracePeriod));
            }

            public static IEnumerable<object[]> RevokedCodeSigningCertificateWithRevocationDateInvalidatesSignaturesData()
            {
                yield return new object[]
                {
                    TimeSpan.FromDays(-1), SignatureDecision.Reject, SignatureDecision.Ignore, SignatureDecision.Ignore
                };

                yield return new object[]
                {
                    TimeSpan.FromDays(0), SignatureDecision.Reject, SignatureDecision.Reject, SignatureDecision.Reject
                };

                yield return new object[]
                {
                    TimeSpan.FromDays(1), SignatureDecision.Reject, SignatureDecision.Reject, SignatureDecision.Reject
                };
            }
        }

        public class MakeDeciderForInvalidatedCertificate : FactsBase
        {
            [Theory]
            [MemberData(nameof(InvalidCertificateInvalidatesSignaturesData))]
            public void InvalidCertificateInvalidatesSignatures(
                EndCertificateUse use,
                X509ChainStatusFlags flags,
                SignatureDecision ingestionDecision,
                SignatureDecision gracePeriodDecision,
                SignatureDecision afterGracePeriodDecision)
            {
                // Arrange
                var certificate = new EndCertificate { Use = use };

                var result = new CertificateVerificationResult(
                                    status: EndCertificateStatus.Invalid,
                                    statusFlags: flags);

                var signatureAtIngestion = new PackageSignature { Status = PackageSignatureStatus.Unknown };
                var signatureAtGracePeriod = new PackageSignature { Status = PackageSignatureStatus.InGracePeriod };
                var signatureAfterGracePeriod = new PackageSignature { Status = PackageSignatureStatus.Valid };

                // Act & Assert
                var decider = _target.MakeDeciderForInvalidatedCertificate(certificate, result);

                Assert.Equal(ingestionDecision, decider(signatureAtIngestion));
                Assert.Equal(gracePeriodDecision, decider(signatureAtGracePeriod));
                Assert.Equal(afterGracePeriodDecision, decider(signatureAfterGracePeriod));
            }

            public static IEnumerable<object[]> InvalidCertificateInvalidatesSignaturesData()
            {
                // HasWeakSignature without NotSignatureValid is rejected at ingestion, but otherwise warned.
                yield return InvalidCertificateInvalidatesSignaturesArguments(
                    use: EndCertificateUse.CodeSigning,
                    flags: X509ChainStatusFlags.HasWeakSignature,
                    ingestionDecision: SignatureDecision.Reject,
                    gracePeriodDecision: SignatureDecision.Warn,
                    afterGracePeriodDecision: SignatureDecision.Warn);

                yield return InvalidCertificateInvalidatesSignaturesArguments(
                    use: EndCertificateUse.Timestamping,
                    flags: X509ChainStatusFlags.HasWeakSignature,
                    ingestionDecision: SignatureDecision.Reject,
                    gracePeriodDecision: SignatureDecision.Warn,
                    afterGracePeriodDecision: SignatureDecision.Warn);

                // HasWeakSignature with NotSignatureValid is rejected at ingestion, but otherwise ignored.
                yield return InvalidCertificateInvalidatesSignaturesArguments(
                    use: EndCertificateUse.CodeSigning,
                    flags: X509ChainStatusFlags.HasWeakSignature | X509ChainStatusFlags.NotSignatureValid,
                    ingestionDecision: SignatureDecision.Reject,
                    gracePeriodDecision: SignatureDecision.Ignore,
                    afterGracePeriodDecision: SignatureDecision.Ignore);

                yield return InvalidCertificateInvalidatesSignaturesArguments(
                    use: EndCertificateUse.Timestamping,
                    flags: X509ChainStatusFlags.HasWeakSignature | X509ChainStatusFlags.NotSignatureValid,
                    ingestionDecision: SignatureDecision.Reject,
                    gracePeriodDecision: SignatureDecision.Ignore,
                    afterGracePeriodDecision: SignatureDecision.Ignore);

                // NotTimeValid is rejected at ingestion, but otherwise ignored.
                yield return InvalidCertificateInvalidatesSignaturesArguments(
                    use: EndCertificateUse.CodeSigning,
                    flags: X509ChainStatusFlags.NotTimeValid,
                    ingestionDecision: SignatureDecision.Reject,
                    gracePeriodDecision: SignatureDecision.Ignore,
                    afterGracePeriodDecision: SignatureDecision.Ignore);

                yield return InvalidCertificateInvalidatesSignaturesArguments(
                    use: EndCertificateUse.Timestamping,
                    flags: X509ChainStatusFlags.NotTimeValid,
                    ingestionDecision: SignatureDecision.Reject,
                    gracePeriodDecision: SignatureDecision.Ignore,
                    afterGracePeriodDecision: SignatureDecision.Ignore);

                // NotTimeValid, HasWeakSignature, and NotSignatureValid are rejected at ingestion, but otherwise ignored.
                yield return InvalidCertificateInvalidatesSignaturesArguments(
                    use: EndCertificateUse.CodeSigning,
                    flags: X509ChainStatusFlags.NotTimeValid | X509ChainStatusFlags.HasWeakSignature | X509ChainStatusFlags.NotSignatureValid,
                    ingestionDecision: SignatureDecision.Reject,
                    gracePeriodDecision: SignatureDecision.Ignore,
                    afterGracePeriodDecision: SignatureDecision.Ignore);

                yield return InvalidCertificateInvalidatesSignaturesArguments(
                    use: EndCertificateUse.Timestamping,
                    flags: X509ChainStatusFlags.NotTimeValid | X509ChainStatusFlags.HasWeakSignature | X509ChainStatusFlags.NotSignatureValid,
                    ingestionDecision: SignatureDecision.Reject,
                    gracePeriodDecision: SignatureDecision.Ignore,
                    afterGracePeriodDecision: SignatureDecision.Ignore);

                // NotTimeNested certificates do not affect dependent signatures.
                yield return InvalidCertificateInvalidatesSignaturesArguments(
                    use: EndCertificateUse.CodeSigning,
                    flags: X509ChainStatusFlags.NotTimeNested,
                    ingestionDecision: SignatureDecision.Ignore,
                    gracePeriodDecision: SignatureDecision.Ignore,
                    afterGracePeriodDecision: SignatureDecision.Ignore);

                yield return InvalidCertificateInvalidatesSignaturesArguments(
                    use: EndCertificateUse.Timestamping,
                    flags: X509ChainStatusFlags.NotTimeNested,
                    ingestionDecision: SignatureDecision.Ignore,
                    gracePeriodDecision: SignatureDecision.Ignore,
                    afterGracePeriodDecision: SignatureDecision.Ignore);

                // Revoked codesigning certificates invalidate all dependent signatures.
                yield return InvalidCertificateInvalidatesSignaturesArguments(
                    use: EndCertificateUse.CodeSigning,
                    flags: X509ChainStatusFlags.Revoked,
                    ingestionDecision: SignatureDecision.Reject,
                    gracePeriodDecision: SignatureDecision.Reject,
                    afterGracePeriodDecision: SignatureDecision.Reject);

                // Revoked timestamping certificates reject signatures at injection, otherwise warn.
                yield return InvalidCertificateInvalidatesSignaturesArguments(
                    use: EndCertificateUse.Timestamping,
                    flags: X509ChainStatusFlags.Revoked,
                    ingestionDecision: SignatureDecision.Reject,
                    gracePeriodDecision: SignatureDecision.Warn,
                    afterGracePeriodDecision: SignatureDecision.Warn);

                // Ensure the following flags are rejected at ingestion but otherwise just warned.
                foreach (var flags in FlagsThatAreRejectedAtIngestionOtherwiseWarn)
                {
                    yield return InvalidCertificateInvalidatesSignaturesArguments(
                        use: EndCertificateUse.CodeSigning,
                        flags: flags,
                        ingestionDecision: SignatureDecision.Reject,
                        gracePeriodDecision: SignatureDecision.Warn,
                        afterGracePeriodDecision: SignatureDecision.Warn);

                    yield return InvalidCertificateInvalidatesSignaturesArguments(
                        use: EndCertificateUse.Timestamping,
                        flags: flags,
                        ingestionDecision: SignatureDecision.Reject,
                        gracePeriodDecision: SignatureDecision.Warn,
                        afterGracePeriodDecision: SignatureDecision.Warn);
                }

                // Ensure the most "drastic" case is always picked from the previous cases.
                yield return InvalidCertificateInvalidatesSignaturesArguments(
                    use: EndCertificateUse.CodeSigning,
                    flags: X509ChainStatusFlags.NotTimeValid | X509ChainStatusFlags.HasWeakSignature | X509ChainStatusFlags.Revoked,
                    ingestionDecision: SignatureDecision.Reject,
                    gracePeriodDecision: SignatureDecision.Reject,
                    afterGracePeriodDecision: SignatureDecision.Reject);

                yield return InvalidCertificateInvalidatesSignaturesArguments(
                    use: EndCertificateUse.CodeSigning,
                    flags: X509ChainStatusFlags.NotTimeNested | X509ChainStatusFlags.Revoked,
                    ingestionDecision: SignatureDecision.Reject,
                    gracePeriodDecision: SignatureDecision.Reject,
                    afterGracePeriodDecision: SignatureDecision.Reject);

                var allFlagsThatAreRejectedAtIngestionOtherwiseWarn = FlagsThatAreRejectedAtIngestionOtherwiseWarn
                                                                        .Aggregate(X509ChainStatusFlags.NoError, (result, next) => result |= next);

                yield return InvalidCertificateInvalidatesSignaturesArguments(
                    use: EndCertificateUse.CodeSigning,
                    flags: allFlagsThatAreRejectedAtIngestionOtherwiseWarn,
                    ingestionDecision: SignatureDecision.Reject,
                    gracePeriodDecision: SignatureDecision.Warn,
                    afterGracePeriodDecision: SignatureDecision.Warn);

                yield return InvalidCertificateInvalidatesSignaturesArguments(
                    use: EndCertificateUse.CodeSigning,
                    flags: allFlagsThatAreRejectedAtIngestionOtherwiseWarn | X509ChainStatusFlags.Revoked,
                    ingestionDecision: SignatureDecision.Reject,
                    gracePeriodDecision: SignatureDecision.Reject,
                    afterGracePeriodDecision: SignatureDecision.Reject);
            }

            private static object[] InvalidCertificateInvalidatesSignaturesArguments(
                EndCertificateUse use,
                X509ChainStatusFlags flags,
                SignatureDecision ingestionDecision,
                SignatureDecision gracePeriodDecision,
                SignatureDecision afterGracePeriodDecision)
            {
                return new object[]
                {
                    use,
                    flags,
                    ingestionDecision,
                    gracePeriodDecision,
                    afterGracePeriodDecision,
                };
            }
        }

        private static X509ChainStatusFlags[] FlagsThatAreRejectedAtIngestionOtherwiseWarn = new[]
        {
            X509ChainStatusFlags.NotSignatureValid,
            X509ChainStatusFlags.NotValidForUsage,
            X509ChainStatusFlags.UntrustedRoot,
            X509ChainStatusFlags.Cyclic,
            X509ChainStatusFlags.InvalidExtension,
            X509ChainStatusFlags.InvalidPolicyConstraints,
            X509ChainStatusFlags.InvalidBasicConstraints,
            X509ChainStatusFlags.InvalidNameConstraints,
            X509ChainStatusFlags.HasNotSupportedNameConstraint,
            X509ChainStatusFlags.HasNotDefinedNameConstraint,
            X509ChainStatusFlags.HasNotPermittedNameConstraint,
            X509ChainStatusFlags.HasExcludedNameConstraint,
            X509ChainStatusFlags.PartialChain,
            X509ChainStatusFlags.CtlNotTimeValid,
            X509ChainStatusFlags.CtlNotSignatureValid,
            X509ChainStatusFlags.CtlNotValidForUsage,
            X509ChainStatusFlags.NoIssuanceChainPolicy,
            X509ChainStatusFlags.ExplicitDistrust,
            X509ChainStatusFlags.HasNotSupportedCriticalExtension,
        };
    }

    public class FactsBase
    {
        protected readonly SignatureDeciderFactory _target;

        public FactsBase()
        {
            _target = new SignatureDeciderFactory();
        }
    }
}
