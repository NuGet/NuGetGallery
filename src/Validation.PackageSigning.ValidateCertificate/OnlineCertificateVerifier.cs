// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using NuGet.Services.Validation;

namespace Validation.PackageSigning.ValidateCertificate
{
    /// <summary>
    /// Performs online revocation verification for a <see cref="X509Certificate2"/>.
    /// </summary>
    /// <remarks>
    /// This depends on Windows' native CryptoApi and will only work on Windows. This
    /// verifier ignores NotTimeValid certificate statuses.
    /// </remarks>
    public class OnlineCertificateVerifier : ICertificateVerifier
    {
        public OnlineCertificateVerifier(
            ILogger<OnlineCertificateVerifier> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// RFC 5280 codeSigning attribute, https://tools.ietf.org/html/rfc5280#section-4.2.1.12
        /// </summary>
        private const string CodeSigningEku = "1.3.6.1.5.5.7.3.3";

        /// <summary>
        /// RFC 3280 "id-kp-timeStamping" https://tools.ietf.org/html/rfc3280.html#section-4.2.1.13
        /// </summary>
        private const string TimeStampingEku = "1.3.6.1.5.5.7.3.8";

        /// <summary>
        /// Chain status flags indicating that the revocation status could not be determined.
        /// </summary>
        private const X509ChainStatusFlags UnknownStatusFlags = X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation;

        /// <summary>
        /// Certificate trust errors indicating that the certificate was not verified online.
        /// </summary>
        private const CertTrustErrorStatus OfflineErrorStatusFlags = CertTrustErrorStatus.CERT_TRUST_REVOCATION_STATUS_UNKNOWN | CertTrustErrorStatus.CERT_TRUST_IS_OFFLINE_REVOCATION;
        
        private readonly ILogger<OnlineCertificateVerifier> _logger;

        public CertificateVerificationResult VerifyCertificate(X509Certificate2 certificate, X509Certificate2[] extraCertificates)
        {
            return VerifyCertificate(certificate, extraCertificates, applicationPolicy: null);
        }

        public CertificateVerificationResult VerifyCodeSigningCertificate(X509Certificate2 certificate, IReadOnlyList<X509Certificate2> extraCertificates)
        {
            return VerifyCertificate(certificate, extraCertificates, applicationPolicy: new Oid(CodeSigningEku));
        }

        public CertificateVerificationResult VerifyTimestampingCertificate(X509Certificate2 certificate, IReadOnlyList<X509Certificate2> extraCertificates)
        {
            return VerifyCertificate(certificate, extraCertificates, applicationPolicy: new Oid(TimeStampingEku));
        }

        private CertificateVerificationResult VerifyCertificate(X509Certificate2 certificate, IReadOnlyList<X509Certificate2> extraCertificates, Oid applicationPolicy)
        {
            _logger.LogInformation("Verifying certificate {SubjectName}, {Thumbprint}",
                certificate.Subject,
                certificate.Thumbprint);
            using (_logger.BeginScope("{SubjectName} {Thumbprint}", certificate.Subject, certificate.Thumbprint))
            {
                X509Chain chain = null;

                try
                {
                    chain = new X509Chain();

                    // Allow the chain to use whatever additional extra certificates were provided.
                    chain.ChainPolicy.ExtraStore.AddRange(extraCertificates.ToArray());

                    if (applicationPolicy != null)
                    {
                        chain.ChainPolicy.ApplicationPolicy.Add(applicationPolicy);
                    }

                    chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                    chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;

                    var resultBuilder = new CertificateVerificationResult.Builder();
                    var chainBuildSucceeded = chain.Build(certificate);
                    if (chainBuildSucceeded)
                    {
                        _logger.LogInformation("Chain.Build() succeeded");
                        resultBuilder.WithStatus(EndCertificateStatus.Good);
                        resultBuilder.WithStatusFlags(X509ChainStatusFlags.NoError);
                        AddRevocationInfo(chain, certificate, resultBuilder);
                    }
                    else
                    {
                        _logger.LogInformation("Chain.Build() failed");
                        resultBuilder.WithStatus(GetEndCertificateStatusFromInvalidChain(chain));
                        resultBuilder.WithStatusFlags(FlattenChainStatusFlags(chain));
                        if (chain.ChainStatus.Length > 0 && chain.ChainElements.Count > 0)
                        {
                            AddRevocationInfo(chain, certificate, resultBuilder);
                        }
                    }

                    return resultBuilder.Build();
                }
                finally
                {
                    if (chain != null)
                    {
                        foreach (var chainElement in chain.ChainElements)
                        {
                            chainElement.Certificate.Dispose();
                        }

                        chain.Dispose();
                    }
                }
            }
        }

        private EndCertificateStatus GetEndCertificateStatusFromInvalidChain(X509Chain chain)
        {
            // ChainStatus and ChainElements properties are expected to be populated
            // even when chain building fails.
            if (chain.ChainStatus.Length == 0)
            {
                _logger.LogWarning("chain.ChainStatus.Length == 0, marking certificate as invalid.");
                return EndCertificateStatus.Invalid;
            }
            if (chain.ChainElements.Count == 0)
            {
                _logger.LogWarning("chain.ChainElements.Count == 0, marking certificate as invalid.");
                return EndCertificateStatus.Invalid;
            }

            // There are multiple reasons why an end certificate may not have a status of EndCertificateStatus.Good:
            //
            // * The end certificate may be revoked or invalid.
            // * An issuing ancestor of the end certificate may be revoked or invalid.
            // * Any combination of the above cases.
            //
            // If the ONLY issue is that the end certificate is revoked, then package signatures created after the revocation will be
            // affected.  In any other case where any certificate has a revoked or invalid status, the end certificate status will be
            // invalid.
            //
            // NOTE: This means that an end certificate that is revoked but has an ignored flag (like "NotTimeNested") will be
            // determined to be invalid here.
            if (OnlyEndCertificateRevokedInInvalidChain(chain))
            {
                _logger.LogWarning("Certificate is revoked.");
                return EndCertificateStatus.Revoked;
            }

            // If ANY status is anything other RevocationStatusUnknown, OfflineRevocation or NoError, the certificate is invalid and
            // dependent signatures should be invalidated.
            if (chain.ChainStatus.Any(s => (s.Status & ~UnknownStatusFlags) != X509ChainStatusFlags.NoError))
            {
                _logger.LogInformation("Error(s) in ChainStatus.");
                return EndCertificateStatus.Invalid;
            }

            // All status flags are RevocationStatusUnknown, OfflineRevocation, or NoError. The certificate's verification should be
            // retried later.
            _logger.LogInformation("Defaulting to 'Unknown' status.");
            return EndCertificateStatus.Unknown;
        }

        private bool OnlyEndCertificateRevokedInInvalidChain(X509Chain chain)
        {
            // Ensure that all of the chain statuses are the Revoked status flag.
            if (chain.ChainStatus.Any(s => (s.Status & ~X509ChainStatusFlags.Revoked) != X509ChainStatusFlags.NoError))
            {
                return false;
            }

            // All chain statuses are Revoked status flags. Ensure that the end certificate has at least one of these Revoked statuses.
            if (!chain.ChainElements[0].ChainElementStatus.Any())
            {
                return false;
            }

            // Ensure that all parent certificates have no errors.
            var parentElements = chain.ChainElements
                                      .Cast<X509ChainElement>()
                                      .Skip(1);

            return (parentElements.All(e => e.ChainElementStatus.All(s => s.Status == X509ChainStatusFlags.NoError)));
        }

        private X509ChainStatusFlags FlattenChainStatusFlags(X509Chain chain)
        {
            var result = X509ChainStatusFlags.NoError;

            if (chain.ChainElements.Count == 0 || chain.ChainStatus.Length == 0)
            {
                // See similar check in GetEndCertificateStatusFromInvalidChain.
                // Even when chain building fails, both ChainElements and ChainStatus are not
                // supposed to be empty. If they are, we will include PartialChain in the list
                // of errors.
                result = X509ChainStatusFlags.PartialChain;
            }
            foreach (var chainStatus in chain.ChainStatus)
            {
                result |= chainStatus.Status;
            }

            return result;
        }

        private unsafe void AddRevocationInfo(X509Chain chain, X509Certificate2 certificate, CertificateVerificationResult.Builder resultBuilder)
        {
            var addedRef = false;
            var chainHandle = chain.SafeHandle;

            try
            {
                chainHandle.DangerousAddRef(ref addedRef);

                CERT_REVOCATION_INFO* pRevocationInfo = GetEndCertificateRevocationInfoPointer(chainHandle, certificate);

                if (CertificateWasVerifiedOnline(pRevocationInfo))
                {
                    resultBuilder.WithRevocationTime(GetRevocationTime(pRevocationInfo));
                    resultBuilder.WithStatusUpdateTime(GetStatusUpdateTime(pRevocationInfo));
                }
            }
            finally
            {
                if (addedRef)
                {
                    chainHandle.DangerousRelease();
                }
            }
        }

        private unsafe CERT_REVOCATION_INFO* GetEndCertificateRevocationInfoPointer(SafeX509ChainHandle chainHandle, X509Certificate2 certificate)
        {
            CERT_CHAIN_CONTEXT* pCertChainContext = (CERT_CHAIN_CONTEXT*)(chainHandle.DangerousGetHandle());

            if (pCertChainContext == null)
            {
                throw new CertificateVerificationException($"Certificate's {certificate.Thumbprint} CERT_CHAIN_CONTEXT* should never be null on Windows");
            }

            if (pCertChainContext->cChain < 1)
            {
                throw new CertificateVerificationException($"Certificate's {certificate.Thumbprint} CERT_CHAIN_CONTEXT* should have at least one chain");
            }

            if (pCertChainContext->rgpChain[0]->cElement < 1)
            {
                throw new CertificateVerificationException($"Certificate's {certificate.Thumbprint} CERT_CHAIN_CONTEXT*'s first chain should have at least one element");
            }

            CERT_SIMPLE_CHAIN* pCertSimpleChain = pCertChainContext->rgpChain[0];
            CERT_CHAIN_ELEMENT* pChainElement = pCertSimpleChain->rgpElement[0];

            return pChainElement->pRevocationInfo;
        }

        private unsafe bool CertificateWasVerifiedOnline(CERT_REVOCATION_INFO* pRevocationInfo)
        {
            if (pRevocationInfo == null || pRevocationInfo->pCrlInfo == null)
            {
                return false;
            }

            return (pRevocationInfo->dwRevocationResult & OfflineErrorStatusFlags) == 0;
        }

        private unsafe DateTime? GetRevocationTime(CERT_REVOCATION_INFO* pRevocationInfo)
        {
            if (pRevocationInfo->dwRevocationResult == CertTrustErrorStatus.CERT_TRUST_NO_ERROR
                || pRevocationInfo->pCrlInfo == null
                || pRevocationInfo->pCrlInfo->pCrlEntry == null)
            {
                return null;
            }

            FILETIME revocationDate = pRevocationInfo->pCrlInfo->pCrlEntry->RevocationDate;

            return revocationDate.ToDateTime().ToUniversalTime();
        }

        private unsafe DateTime? GetStatusUpdateTime(CERT_REVOCATION_INFO* pRevocationInfo)
        {
            CERT_REVOCATION_CRL_INFO* pCrlInfo = pRevocationInfo->pCrlInfo;
            if (pCrlInfo == null)
            {
                return null;
            }

            if (pCrlInfo->pDeltaCRLContext != null && pCrlInfo->pDeltaCRLContext->pCrlInfo != null)
            {
                FILETIME statusUpdate = pCrlInfo->pDeltaCRLContext->pCrlInfo->ThisUpdate;

                return statusUpdate.ToDateTime().ToUniversalTime();
            }

            if (pCrlInfo->pBaseCRLContext != null && pCrlInfo->pBaseCRLContext->pCrlInfo != null)
            {
               FILETIME statusUpdate = pCrlInfo->pBaseCRLContext->pCrlInfo->ThisUpdate;

               return statusUpdate.ToDateTime().ToUniversalTime();
            }

            return null;
        }
    }
}