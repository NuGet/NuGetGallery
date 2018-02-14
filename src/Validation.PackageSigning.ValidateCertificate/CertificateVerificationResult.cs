// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using NuGet.Services.Validation;

namespace Validation.PackageSigning.ValidateCertificate
{
    /// <summary>
    /// The result of a <see cref="X509Certificate2"/> verification by the
    /// <see cref="ICertificateValidationService"/>.
    /// </summary>
    public class CertificateVerificationResult
    {
        /// <summary>
        /// Create a new verification result.
        /// </summary>
        /// <param name="status">The determined status of the verified certificate.</param>
        /// <param name="statusFlags">The flags explaining the certificate's status.</param>
        /// <param name="statusUpdateTime">The time the revocation info was published.</param>
        /// <param name="revocationTime">The date the certificate was revoked, if applicable.</param>
        public CertificateVerificationResult(
            EndCertificateStatus status,
            X509ChainStatusFlags statusFlags,
            DateTime? statusUpdateTime = null,
            DateTime? revocationTime = null)
        {
            if (revocationTime.HasValue &&
                status != EndCertificateStatus.Revoked &&
                status != EndCertificateStatus.Invalid)
            {
                throw new ArgumentException(
                    $"End certificate revoked at {revocationTime} but status is {status}",
                    nameof(status));
            }

            switch (status)
            {
                case EndCertificateStatus.Good:
                    if (statusFlags != X509ChainStatusFlags.NoError)
                    {
                        throw new ArgumentException(
                            $"Invalid flags '{statusFlags}' for status '{status}'",
                            nameof(statusFlags));
                    }

                    break;

                case EndCertificateStatus.Invalid:
                    if (statusFlags == X509ChainStatusFlags.NoError)
                    {
                        throw new ArgumentException(
                            $"Invalid flags '{statusFlags}' for status '{status}'",
                            nameof(statusFlags));
                    }

                    break;

                case EndCertificateStatus.Revoked:
                    if ((statusFlags & X509ChainStatusFlags.Revoked) == 0)
                    {
                        throw new ArgumentException(
                            $"Invalid flags '{statusFlags}' for status '{status}'",
                            nameof(statusFlags));
                    }
                    break;

                case EndCertificateStatus.Unknown:
                    if ((statusFlags & (X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation)) == 0)
                    {
                        throw new ArgumentException(
                            $"Invalid flags '{statusFlags}' for status '{status}'",
                            nameof(statusFlags));
                    }

                    break;

                default:
                    throw new ArgumentException($"Unknown status '{status}'", nameof(status));
            }

            Status = status;
            StatusFlags = statusFlags;
            StatusUpdateTime = statusUpdateTime;
            RevocationTime = revocationTime;
        }

        /// <summary>
        /// The status of the end <see cref="X509Certificate2"/>.
        /// </summary>
        public EndCertificateStatus Status { get; }

        /// <summary>
        /// The flattened flags for the <see cref="X509Certificate2"/>'s entire chain.
        /// </summary>
        public X509ChainStatusFlags StatusFlags { get; }

        /// <summary>
        /// The time that the end <see cref="X509Certificate2"/>'s status was last updated, according to the
        /// Certificate Authority. This value may be <c>null</c> if the <see cref="Status"/> is
        /// <see cref="EndCertificateStatus.Unknown"/> or if the status could not be determined.
        /// </summary>
        public DateTime? StatusUpdateTime { get; }

        /// <summary>
        /// The time at which the end <see cref="X509Certificate2"/> was revoked. If <see cref="Status"/>
        /// is not <see cref="CertificateStatus.Revoked"/>, this will have a value of <c>null</c>.
        /// </summary>
        public DateTime? RevocationTime { get; }

        /// <summary>
        /// Convert a verification to a human readable string.
        /// </summary>
        /// <returns>A human readable string that summarizes the verification result.</returns>
        public override string ToString()
        {
            switch (Status)
            {
                case EndCertificateStatus.Good:
                    return $"Good (StatusUpdateTime = {StatusUpdateTime})";

                case EndCertificateStatus.Invalid:
                    return $"Invalid (Flags = {StatusFlags}, RevocationTime = {RevocationTime}, StatusUpdateTime = {StatusUpdateTime})";

                case EndCertificateStatus.Revoked:
                    return $"Revoked (Flags = {StatusFlags}, RevocationTime = {RevocationTime}, StatusUpdateTime = {StatusUpdateTime})";

                case EndCertificateStatus.Unknown:
                    return $"Unknown (Flags = {StatusFlags}, StatusUpdateTime = {StatusUpdateTime})";

                default:
                    throw new InvalidOperationException($"Unknown status {Status}");
            }
        }

        /// <summary>
        /// Helper used to create valid <see cref="CertificateVerificationResult"/>s.
        /// </summary>
        public class Builder
        {
            private EndCertificateStatus _status;
            private X509ChainStatusFlags _statusFlags;
            private DateTime? _statusUpdateTime;
            private DateTime? _revocationTime;

            public Builder WithStatus(EndCertificateStatus value)
            {
                _status = value;
                return this;
            }

            public Builder WithStatusFlags(X509ChainStatusFlags value)
            {
                _statusFlags = value;
                return this;
            }

            public Builder WithStatusUpdateTime(DateTime? value)
            {
                _statusUpdateTime = value;
                return this;
            }

            public Builder WithRevocationTime(DateTime? value)
            {
                _revocationTime = value;
                return this;
            }

            public CertificateVerificationResult Build()
            {
                return new CertificateVerificationResult(
                    _status,
                    _statusFlags,
                    _statusUpdateTime,
                    _revocationTime);
            }
        }
    }
}
