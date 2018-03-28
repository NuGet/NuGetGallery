// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace NuGet.Services.Validation
{
    public static class PackageSignatureExtensions
    {
        /// <summary>
        /// Decide whether the valid signature should be considered "Valid" or "InGracePeriod".
        /// </summary>
        /// <param name="request">The validation request for the package whose signature should be inspected.</param>
        /// <param name="signature">The valid signature whose status should be decided.</param>
        /// <returns>True if the signature should be "Valid", false if it should be "InGracePeriod".</returns>
        public static bool IsPromotable(this PackageSignature signature)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            if (signature.Status == PackageSignatureStatus.Invalid)
            {
                throw new ArgumentException($"Package signature {signature.Key} is invalid and cannot be promoted", nameof(signature));
            }

            var signingTime = signature.TrustedTimestamps.Max(t => t.Value);

            // Ensure the timestamps' certificate statuses are fresher than the signature.
            foreach (var timestamp in signature.TrustedTimestamps)
            {
                // A valid signature should NEVER have a timestamp whose end certificate is revoked.
                // Note that it is possible for a valid signature to have an invalid certificate as
                // certain certificate statuses, like "NotTimeNested", do not affect signatures.
                if (timestamp.EndCertificate.Status == EndCertificateStatus.Revoked)
                {
                    throw new ArgumentException(
                        $"Package signature {signature.Key} is valid but has a timestamp whose end certificate is revoked",
                        nameof(signature));
                }

                if (!IsCertificateStatusPastTime(timestamp.EndCertificate, signingTime))
                {
                    return false;
                }
            }

            // A signature can be valid even if its certificate is revoked as long as the certificate
            // revocation date begins after the signature was created. The validation pipeline does
            // not revalidate revoked certificates, thus, a valid package signature with a revoked
            // certificate is considered out of the grace period regardless of the certificate's
            // status update time.
            if (signature.EndCertificate.Status != EndCertificateStatus.Revoked)
            {
                // Ensure the signature's certificate status is fresher than the signature.
                if (!IsCertificateStatusPastTime(signature.EndCertificate, signingTime))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsCertificateStatusPastTime(EndCertificate certificate, DateTime time)
        {
            return (certificate.StatusUpdateTime.HasValue && certificate.StatusUpdateTime > time);
        }
    }
}
