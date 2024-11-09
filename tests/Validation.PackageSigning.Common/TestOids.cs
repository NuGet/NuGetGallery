// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    public static class TestOids
    {
        public static readonly Oid AnyEku = new(DottedDecimals.AnyEku);
        internal static readonly Oid AuthorityInfoAccess = new(DottedDecimals.AuthorityInfoAccess);
        internal static readonly Oid AuthorityKeyIdentifier = new(DottedDecimals.AuthorityKeyIdentifier);
        internal static readonly Oid BasicConstraints2 = new(DottedDecimals.BasicConstraints2);
        internal static readonly Oid CaIssuers = new(DottedDecimals.CaIssuers);
        public static readonly Oid ClientAuthenticationEku = new(DottedDecimals.ClientAuthenticationEku);
        public static readonly Oid CommitmentTypeIdentifierProofOfOrigin = new(DottedDecimals.CommitmentTypeIdentifierProofOfOrigin);
        public static readonly Oid CommitmentTypeIdentifierProofOfReceipt = new(DottedDecimals.CommitmentTypeIdentifierProofOfReceipt);
        public static readonly Oid CommitmentTypeIdentifierProofOfDelivery = new(DottedDecimals.CommitmentTypeIdentifierProofOfDelivery);
        public static readonly Oid CommitmentTypeIdentifierProofOfSender = new(DottedDecimals.CommitmentTypeIdentifierProofOfSender);
        public static readonly Oid CommitmentTypeIdentifierProofOfApproval = new(DottedDecimals.CommitmentTypeIdentifierProofOfApproval);
        internal static readonly Oid CommonName = new(DottedDecimals.CommonName);
        public static readonly Oid Countersignature = new(DottedDecimals.Countersignature);
        internal static readonly Oid CountryOrRegionName = new(DottedDecimals.CountryOrRegionName);
        internal static readonly Oid CrlDistributionPoints = new(DottedDecimals.CrlDistributionPoints);
        internal static readonly Oid CrlNumber = new(DottedDecimals.CrlNumber);
        internal static readonly Oid CrlReasons = new(DottedDecimals.CrlReasons);
        internal static readonly Oid EcPublicKey = new(DottedDecimals.EcPublicKey);
        internal static readonly Oid EmailAddress = new(DottedDecimals.EmailAddress);
        public static readonly Oid EmailProtectionEku = new(DottedDecimals.EmailProtectionEku);
        internal static readonly Oid KeyUsage = new(DottedDecimals.KeyUsage);
        internal static readonly Oid LocalityName = new(DottedDecimals.LocalityName);
        internal static readonly Oid Ocsp = new(DottedDecimals.Ocsp);
        internal static readonly Oid OcspBasic = new(DottedDecimals.OcspBasic);
        internal static readonly Oid OcspNonce = new(DottedDecimals.OcspNonce);
        internal static readonly Oid Organization = new(DottedDecimals.Organization);
        internal static readonly Oid OrganizationalUnit = new(DottedDecimals.OrganizationalUnit);
        internal static readonly Oid Rsa = new(DottedDecimals.Rsa);
        public static readonly Oid Sha256 = new(DottedDecimals.Sha256);
        public static readonly Oid Sha384 = new(DottedDecimals.Sha384);
        public static readonly Oid Sha512 = new(DottedDecimals.Sha512);
        internal static readonly Oid Sha256WithRSAEncryption = new(DottedDecimals.Sha256WithRSAEncryption);
        public static readonly Oid SignatureTimestampToken = new(DottedDecimals.SignatureTimestampToken);
        public static readonly Oid SignedData = new(DottedDecimals.SignedData);
        internal static readonly Oid StateOrProvinceName = new(DottedDecimals.StateOrProvinceName);
        internal static readonly Oid SubjectKeyIdentifier = new(DottedDecimals.SubjectKeyIdentifier);

        private static class DottedDecimals
        {
            internal const string EcPublicKey = "1.2.840.10045.2.1";
            internal const string Rsa = "1.2.840.113549.1.1.1";
            internal const string SignedData = "1.2.840.113549.1.7.2";
            internal const string EmailAddress = "1.2.840.113549.1.9.1";
            internal const string Countersignature = "1.2.840.113549.1.9.6";
            internal const string SignatureTimestampToken = "1.2.840.113549.1.9.16.2.14";
            internal const string CommitmentTypeIdentifierProofOfOrigin = "1.2.840.113549.1.9.16.6.1";
            internal const string CommitmentTypeIdentifierProofOfReceipt = "1.2.840.113549.1.9.16.6.2";
            internal const string CommitmentTypeIdentifierProofOfDelivery = "1.2.840.113549.1.9.16.6.3";
            internal const string CommitmentTypeIdentifierProofOfSender = "1.2.840.113549.1.9.16.6.4";
            internal const string CommitmentTypeIdentifierProofOfApproval = "1.2.840.113549.1.9.16.6.5";
            internal const string AuthorityInfoAccess = "1.3.6.1.5.5.7.1.1";
            internal const string ClientAuthenticationEku = "1.3.6.1.5.5.7.3.2";
            internal const string EmailProtectionEku = "1.3.6.1.5.5.7.3.4";
            internal const string Ocsp = "1.3.6.1.5.5.7.48.1";
            internal const string OcspBasic = "1.3.6.1.5.5.7.48.1.1";
            internal const string OcspNonce = "1.3.6.1.5.5.7.48.1.2";
            internal const string CaIssuers = "1.3.6.1.5.5.7.48.2";
            internal const string CommonName = "2.5.4.3";
            internal const string CountryOrRegionName = "2.5.4.6";
            internal const string LocalityName = "2.5.4.7";
            internal const string StateOrProvinceName = "2.5.4.8";
            internal const string Organization = "2.5.4.10";
            internal const string OrganizationalUnit = "2.5.4.11";
            internal const string SubjectKeyIdentifier = "2.5.29.14";
            internal const string KeyUsage = "2.5.29.15";
            internal const string BasicConstraints2 = "2.5.29.19";
            internal const string CrlNumber = "2.5.29.20";
            internal const string CrlReasons = "2.5.29.21";
            internal const string CrlDistributionPoints = "2.5.29.31";
            internal const string AuthorityKeyIdentifier = "2.5.29.35";
            internal const string AnyEku = "2.5.29.37.0";
            internal const string Sha256 = "2.16.840.1.101.3.4.2.1";
            internal const string Sha384 = "2.16.840.1.101.3.4.2.2";
            internal const string Sha512 = "2.16.840.1.101.3.4.2.3";
            internal const string Sha256WithRSAEncryption = "1.2.840.113549.1.1.11";
        }
    }
}
