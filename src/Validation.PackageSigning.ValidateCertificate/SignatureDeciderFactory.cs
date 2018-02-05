// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using NuGet.Services.Validation;

namespace Validation.PackageSigning.ValidateCertificate
{
    /// <summary>
    /// Deciders are created from a given <see cref="CertificateVerificationResult"/> to decide
    /// how each of the certificate's dependent <see cref="PackageSignature"/>s should be affected.
    /// </summary>
    /// <param name="signature">A signature that depends on the <see cref="EndCertificate"/> that was verified.</param>
    /// <returns>How the signature should be handled.</returns>
    public delegate SignatureDecision SignatureDecider(PackageSignature signature);

    /// <summary>
    /// Creates functions that decide how <see cref="PackageSignature"/>s should be affected by
    /// <see cref="EndCertificate.Status"/> changes.
    /// </summary>
    public class SignatureDeciderFactory
    {
        /// <summary>
        /// Create a function to decide how signatures should be affected by a revoked certificate.
        /// </summary>
        /// <param name="certificate">The certificate that was revoked.</param>
        /// <param name="result">The verification result that describes when the certificate was revoked.</param>
        /// <returns>The function that describes how dependent signatures should be affected by the certificate status change.</returns>
        public SignatureDecider MakeDeciderForRevokedCertificate(EndCertificate certificate, CertificateVerificationResult result)
        {
            switch (certificate.Use)
            {
                case EndCertificateUse.Timestamping:
                    return RejectSignaturesAtIngestionOtherwiseWarnDecider;

                case EndCertificateUse.CodeSigning:
                    return MakeDeciderForRevokedCodeSigningCertificate(result.RevocationTime);

                default:
                    throw new InvalidOperationException($"Revoked certificate has unknown use: {certificate.Use}");
            }
        }

        /// <summary>
        /// Create a function to decide how signatures should be affected by an invalidated certificate.
        /// </summary>
        /// <param name="certificate">The certificate that was invalidated.</param>
        /// <param name="result">The verification result that describes why the certificate was invalidated.</param>
        /// <returns>The function that describes how dependent signatures should be affected by the certificate status change.</returns>
        public SignatureDecider MakeDeciderForInvalidatedCertificate(EndCertificate certificate, CertificateVerificationResult result)
        {
            if (result.Status != EndCertificateStatus.Invalid)
            {
                throw new ArgumentException($"Result must have a status of {nameof(EndCertificateStatus.Invalid)}", nameof(result));
            }

            if (result.StatusFlags == X509ChainStatusFlags.NoError ||
                (result.StatusFlags & (X509ChainStatusFlags.OfflineRevocation | X509ChainStatusFlags.RevocationStatusUnknown)) != 0)
            {
                throw new ArgumentException($"Invalid flags on invalid verification result: {result.StatusFlags}!", nameof(result));
            }

            // If a certificate used for the primary signature is revoked, all dependent signatures should be invalidated.
            // Note that in this case the revoked certificate is a parent of the end certificate - NOT the end certificate.
            if (certificate.Use == EndCertificateUse.CodeSigning && (result.StatusFlags & X509ChainStatusFlags.Revoked) != 0)
            {
                return RejectAllSignaturesDecider;
            }

            // NotTimeValid and HasWeakSignature fail packages only at ingestion.
            else if (ResultHasOnlyFlags(result, X509ChainStatusFlags.NotTimeValid | X509ChainStatusFlags.HasWeakSignature))
            {
                return RejectSignaturesAtIngestionDecider;
            }

            // NotTimeNested does not affect signatures and should be ignored.
            else if (ResultHasOnlyFlags(result, X509ChainStatusFlags.NotTimeNested))
            {
                return NoActionDecider;
            }

            // In all other cases, reject signatures at ingestion and warn on all other signatures.
            else
            {
                return RejectSignaturesAtIngestionOtherwiseWarnDecider;
            }
        }

        private SignatureDecider MakeDeciderForRevokedCodeSigningCertificate(DateTime? revocationTime)
        {
            // If the time the certificate was revoked is unknown, assume the worst and reject all dependent signatures.
            if (!revocationTime.HasValue)
            {
                return RejectAllSignaturesDecider;
            }

            return (PackageSignature signature) =>
            {
                // The revoked code signing certificate invalidates signatures with no valid timestamps.
                // TODO: This should skip trusted timestamps with invalid/revoked certs. This requires:
                //          * Getting trusted timestamps' certificates and their statuses.
                if (!signature.TrustedTimestamps.Any())
                {
                    return SignatureDecision.Reject;
                }

                // The revoked code signing certificate invalidates signatures with at least one trusted
                // timestamp before the revocation date.
                if (signature.TrustedTimestamps.Any(t => revocationTime.Value <= t.Value))
                {
                    return SignatureDecision.Reject;
                }

                // This signature lives to see another day.
                return SignatureDecision.Ignore;
            };
        }

        private SignatureDecision NoActionDecider(PackageSignature signature) => SignatureDecision.Ignore;

        private SignatureDecision RejectAllSignaturesDecider(PackageSignature signature) => SignatureDecision.Reject;

        private SignatureDecision RejectSignaturesAtIngestionDecider(PackageSignature signature)
        {
            return (signature.Status == PackageSignatureStatus.Unknown)
                        ? SignatureDecision.Reject
                        : SignatureDecision.Ignore;
        }

        private SignatureDecision RejectSignaturesAtIngestionOtherwiseWarnDecider(PackageSignature signature)
        {
            return (signature.Status == PackageSignatureStatus.Unknown)
                    ? SignatureDecision.Reject
                    : SignatureDecision.Warn;
        }

        private bool ResultHasOnlyFlags(CertificateVerificationResult result, X509ChainStatusFlags flags)
        {
            if (result.StatusFlags == X509ChainStatusFlags.NoError)
            {
                return flags == X509ChainStatusFlags.NoError;
            }

            return (result.StatusFlags & flags) == result.StatusFlags;
        }
    }
}
