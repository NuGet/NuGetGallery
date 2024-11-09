// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    // https://tools.ietf.org/html/rfc6960
    public sealed class OcspResponder : HttpResponder
    {
        private const string RequestContentType = "application/ocsp-request";
        private const string ResponseContentType = "application/ocsp-response";

        private readonly OcspResponderOptions _options;
        private readonly ConcurrentDictionary<string, DateTimeOffset> _responses;

        public override Uri Url { get; }

        internal CertificateAuthority CertificateAuthority { get; }

        private OcspResponder(CertificateAuthority certificateAuthority, OcspResponderOptions options)
        {
            CertificateAuthority = certificateAuthority;
            Url = certificateAuthority.OcspResponderUri;
            _options = options;
            _responses = new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        }

        public static OcspResponder Create(
            CertificateAuthority certificateAuthority,
            OcspResponderOptions? options = null)
        {
            if (certificateAuthority == null)
            {
                throw new ArgumentNullException(nameof(certificateAuthority));
            }

            options = options ?? new OcspResponderOptions();

            return new OcspResponder(certificateAuthority, options);
        }

        public override void Respond(HttpListenerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            byte[]? bytes = GetOcspRequest(context);

            if (bytes is null)
            {
                context.Response.StatusCode = 400;

                return;
            }

            OcspRequest ocspRequest = OcspRequest.Decode(bytes);
            OcspResponse ocspResponse = BuildOcspResponse(
                ocspRequest,
                out List<X509Certificate2> certificateChain,
                out CertStatus certStatus);
            AsnWriter writer = new(AsnEncodingRules.DER);

            ocspResponse.Encode(writer);

            bytes = writer.Encode();

            context.Response.ContentType = ResponseContentType;

            WriteResponseBody(context.Response, bytes);
        }

        private static byte[]? GetOcspRequest(HttpListenerContext context)
        {
            // See https://tools.ietf.org/html/rfc6960#appendix-A.
            if (IsGet(context.Request))
            {
                string path = context.Request.RawUrl!;
                string urlEncoded = path.Substring(path.IndexOf('/', 1)).TrimStart('/');
                string base64 = WebUtility.UrlDecode(urlEncoded);

                return Convert.FromBase64String(base64);
            }

            if (IsPost(context.Request) &&
                string.Equals(context.Request.ContentType, RequestContentType, StringComparison.OrdinalIgnoreCase))
            {
                return ReadRequestBody(context.Request);
            }

            return null;
        }

        public Task WaitForResponseExpirationAsync(X509Certificate2 certificate)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            if (_responses.TryGetValue(certificate.SerialNumber, out var nextUpdate))
            {
                // Ensure expiration
                var delay = nextUpdate.AddSeconds(1) - DateTimeOffset.UtcNow;

                if (delay > TimeSpan.Zero)
                {
                    return Task.Delay(delay);
                }
            }

            return Task.CompletedTask;
        }

        internal OcspResponse BuildOcspResponse(
            OcspRequest ocspRequest,
            out List<X509Certificate2> certificateChain,
            out CertStatus certStatus)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset thisUpdate = _options.ThisUpdate ?? now;
            //On Windows, if the current time is equal (to the second) to a notAfter time (or nextUpdate time), it's considered valid.
            //But OpenSSL considers it already expired (that the expiry happened when the clock changed to this second)
            DateTimeOffset nextUpdate = _options.NextUpdate ?? now.AddSeconds(10);

            Request request = ocspRequest.TbsRequest.RequestList[0];
            X509ExtensionAsn? nonceExtension = GetNonceExtension(request);
            CertStatus status = CertificateAuthority.GetCertStatus(request.CertId, out X509Certificate2 certificate);
            certStatus = status;
            X509Certificate2 responder = CertificateAuthority.Certificate;
            List<X509ExtensionAsn> singleExtensions = new();
            List<SingleResponse> singleResponses = new()
            {
                new SingleResponse(request.CertId, status, thisUpdate, nextUpdate, singleExtensions)
            };
            List<X509ExtensionAsn> responseExtensions = new();

            if (nonceExtension is not null)
            {
                responseExtensions.Add(nonceExtension.Value);
            }

            ResponseData responseData = new(
                BigInteger.One,
                new ResponderId(responder.SubjectName),
                now,
                singleResponses,
                responseExtensions);
            AlgorithmIdentifier signatureAlgorithm = new(TestOids.Sha256WithRSAEncryption);
            ReadOnlyMemory<byte> signature = Sign(responseData, HashAlgorithmName.SHA256);

            List<X509Certificate2> certs = GetCertificateChain();
            certificateChain = certs;
            BasicOcspResponse basicResponse = new(responseData, signatureAlgorithm, signature, certs);
            ResponseBytes responseBytes = ResponseBytes.From(basicResponse);
            OcspResponse response = new(OcspResponseStatus.Successful, responseBytes);

            _responses.AddOrUpdate(certificate.SerialNumber, nextUpdate, (key, currentNextUpdate) =>
            {
                if (nextUpdate > currentNextUpdate)
                {
                    return nextUpdate;
                }

                return currentNextUpdate;
            });

            return response;
        }

        private List<X509Certificate2> GetCertificateChain()
        {
            List<X509Certificate2> certificates = new();
            CertificateAuthority? certificateAuthority = CertificateAuthority;

            while (certificateAuthority is not null)
            {
                certificates.Add(certificateAuthority.Certificate);

                certificateAuthority = certificateAuthority.Parent;
            }

            return certificates;
        }

        private static X509ExtensionAsn? GetNonceExtension(Request request)
        {
            if (request.SingleRequestExtensions is not null)
            {
                foreach (X509ExtensionAsn extension in request.SingleRequestExtensions)
                {
                    if (string.Equals(TestOids.OcspNonce.Value, extension.ExtnId))
                    {
                        return extension;
                    }
                }
            }

            return null;
        }

        private ReadOnlyMemory<byte> Sign(ResponseData responseData, HashAlgorithmName hashAlgorithmName)
        {
            AsnWriter writer = new(AsnEncodingRules.DER);

            responseData.Encode(writer);

            byte[] tbsResponseData = writer.Encode();

            // CodeQL [SM03799] This is test code. This is a test OCSP responder for local testing of various signing and verification scenarios in the product. We need to support the default for CMS and X.509 signing, which is PKCS #1 v1.5. See internal bug 2287166.
            return CertificateAuthority.KeyPair.SignData(tbsResponseData, hashAlgorithmName, RSASignaturePadding.Pkcs1);
        }
    }
}
