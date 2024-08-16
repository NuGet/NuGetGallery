// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Validation.PackageSigning.ValidateCertificate
{
    // This file contains types that map to Window's native CryptoAPI, and is
    // based off of .NET's CoreFX: https://github.com/dotnet/corefx/blob/master/src/System.Security.Cryptography.X509Certificates/src/Internal/Cryptography/Pal.Windows/Native/Primitives.cs
    // CryptoAPI documentation: https://msdn.microsoft.com/en-us/library/windows/desktop/aa380256(v=vs.85).aspx
    // Additions/modifications to CoreFX's code are marked with "DIFFERENT FROM COREFX"

    internal enum CertEncodingType : int
    {
        PKCS_7_ASN_ENCODING = 0x10000,
        X509_ASN_ENCODING = 0x00001,

        All = PKCS_7_ASN_ENCODING | X509_ASN_ENCODING,
    }

    internal enum ContentType : int
    {
        //encoded single certificate
        CERT_QUERY_CONTENT_CERT = 1,
        //encoded single CTL
        CERT_QUERY_CONTENT_CTL = 2,
        //encoded single CRL
        CERT_QUERY_CONTENT_CRL = 3,
        //serialized store
        CERT_QUERY_CONTENT_SERIALIZED_STORE = 4,
        //serialized single certificate
        CERT_QUERY_CONTENT_SERIALIZED_CERT = 5,
        //serialized single CTL
        CERT_QUERY_CONTENT_SERIALIZED_CTL = 6,
        //serialized single CRL
        CERT_QUERY_CONTENT_SERIALIZED_CRL = 7,
        //a PKCS#7 signed message
        CERT_QUERY_CONTENT_PKCS7_SIGNED = 8,
        //a PKCS#7 message, such as enveloped message.  But it is not a signed message,
        CERT_QUERY_CONTENT_PKCS7_UNSIGNED = 9,
        //a PKCS7 signed message embedded in a file
        CERT_QUERY_CONTENT_PKCS7_SIGNED_EMBED = 10,
        //an encoded PKCS#10
        CERT_QUERY_CONTENT_PKCS10 = 11,
        //an encoded PFX BLOB
        CERT_QUERY_CONTENT_PFX = 12,
        //an encoded CertificatePair (contains forward and/or reverse cross certs)
        CERT_QUERY_CONTENT_CERT_PAIR = 13,
        //an encoded PFX BLOB, which was loaded to phCertStore
        CERT_QUERY_CONTENT_PFX_AND_LOAD = 14,
    }

    internal enum FormatType : int
    {
        CERT_QUERY_FORMAT_BINARY = 1,
        CERT_QUERY_FORMAT_BASE64_ENCODED = 2,
        CERT_QUERY_FORMAT_ASN_ASCII_HEX_ENCODED = 3,
    }

    // CRYPTOAPI_BLOB has many typedef aliases in the C++ world (CERT_BLOB, DATA_BLOB, etc.) We'll just stick to one name here.
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct CRYPTOAPI_BLOB
    {
        public CRYPTOAPI_BLOB(int cbData, byte* pbData)
        {
            this.cbData = cbData;
            this.pbData = pbData;
        }

        public int cbData;
        public byte* pbData;

        public byte[] ToByteArray()
        {
            if (cbData == 0)
            {
                return Array.Empty<byte>();
            }

            byte[] array = new byte[cbData];
            Marshal.Copy((IntPtr)pbData, array, 0, cbData);
            return array;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct CERT_CONTEXT
    {
        public CertEncodingType dwCertEncodingType;
        public byte* pbCertEncoded;
        public int cbCertEncoded;
        public CERT_INFO* pCertInfo;
        public IntPtr hCertStore;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct CERT_INFO
    {
        public int dwVersion;
        public CRYPTOAPI_BLOB SerialNumber;
        public CRYPT_ALGORITHM_IDENTIFIER SignatureAlgorithm;
        public CRYPTOAPI_BLOB Issuer;
        public FILETIME NotBefore;
        public FILETIME NotAfter;
        public CRYPTOAPI_BLOB Subject;
        public CERT_PUBLIC_KEY_INFO SubjectPublicKeyInfo;
        public CRYPT_BIT_BLOB IssuerUniqueId;
        public CRYPT_BIT_BLOB SubjectUniqueId;
        public int cExtension;
        public CERT_EXTENSION* rgExtension;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CRYPT_ALGORITHM_IDENTIFIER
    {
        public IntPtr pszObjId;
        public CRYPTOAPI_BLOB Parameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CERT_PUBLIC_KEY_INFO
    {
        public CRYPT_ALGORITHM_IDENTIFIER Algorithm;
        public CRYPT_BIT_BLOB PublicKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct CRYPT_BIT_BLOB
    {
        public int cbData;
        public byte* pbData;
        public int cUnusedBits;

        public byte[] ToByteArray()
        {
            if (cbData == 0)
            {
                return Array.Empty<byte>();
            }

            byte[] array = new byte[cbData];
            Marshal.Copy((IntPtr)pbData, array, 0, cbData);
            return array;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct CERT_EXTENSION
    {
        public IntPtr pszObjId;
        public int fCritical;
        public CRYPTOAPI_BLOB Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FILETIME
    {
        private uint ftTimeLow;
        private uint ftTimeHigh;

        public DateTime ToDateTime()
        {
            long fileTime = (((long)ftTimeHigh) << 32) + ftTimeLow;
            return DateTime.FromFileTime(fileTime);
        }

        public static FILETIME FromDateTime(DateTime dt)
        {
            long fileTime = dt.ToFileTime();

            unchecked
            {
                return new FILETIME()
                {
                    ftTimeLow = (uint)fileTime,
                    ftTimeHigh = (uint)(fileTime >> 32),
                };
            }
        }
    }

    // DIFFERENT FROM COREFX (Changed enum type from int to uint)
    [Flags]
    internal enum CertTrustErrorStatus : uint
    {
        CERT_TRUST_NO_ERROR = 0x00000000,
        CERT_TRUST_IS_NOT_TIME_VALID = 0x00000001,
        CERT_TRUST_IS_NOT_TIME_NESTED = 0x00000002,
        CERT_TRUST_IS_REVOKED = 0x00000004,
        CERT_TRUST_IS_NOT_SIGNATURE_VALID = 0x00000008,
        CERT_TRUST_IS_NOT_VALID_FOR_USAGE = 0x00000010,
        CERT_TRUST_IS_UNTRUSTED_ROOT = 0x00000020,
        CERT_TRUST_REVOCATION_STATUS_UNKNOWN = 0x00000040,
        CERT_TRUST_IS_CYCLIC = 0x00000080,

        CERT_TRUST_INVALID_EXTENSION = 0x00000100,
        CERT_TRUST_INVALID_POLICY_CONSTRAINTS = 0x00000200,
        CERT_TRUST_INVALID_BASIC_CONSTRAINTS = 0x00000400,
        CERT_TRUST_INVALID_NAME_CONSTRAINTS = 0x00000800,
        CERT_TRUST_HAS_NOT_SUPPORTED_NAME_CONSTRAINT = 0x00001000,
        CERT_TRUST_HAS_NOT_DEFINED_NAME_CONSTRAINT = 0x00002000,
        CERT_TRUST_HAS_NOT_PERMITTED_NAME_CONSTRAINT = 0x00004000,
        CERT_TRUST_HAS_EXCLUDED_NAME_CONSTRAINT = 0x00008000,

        CERT_TRUST_IS_OFFLINE_REVOCATION = 0x01000000,
        CERT_TRUST_NO_ISSUANCE_CHAIN_POLICY = 0x02000000,
        CERT_TRUST_IS_EXPLICIT_DISTRUST = 0x04000000,
        CERT_TRUST_HAS_NOT_SUPPORTED_CRITICAL_EXT = 0x08000000,
        CERT_TRUST_HAS_WEAK_SIGNATURE = 0x00100000,

        // These can be applied to chains only
        CERT_TRUST_IS_PARTIAL_CHAIN = 0x00010000,
        CERT_TRUST_CTL_IS_NOT_TIME_VALID = 0x00020000,
        CERT_TRUST_CTL_IS_NOT_SIGNATURE_VALID = 0x00040000,
        CERT_TRUST_CTL_IS_NOT_VALID_FOR_USAGE = 0x00080000,
    }

    [Flags]
    internal enum CertTrustInfoStatus : int
    {
        // These can be applied to certificates only
        CERT_TRUST_HAS_EXACT_MATCH_ISSUER = 0x00000001,
        CERT_TRUST_HAS_KEY_MATCH_ISSUER = 0x00000002,
        CERT_TRUST_HAS_NAME_MATCH_ISSUER = 0x00000004,
        CERT_TRUST_IS_SELF_SIGNED = 0x00000008,

        // These can be applied to certificates and chains
        CERT_TRUST_HAS_PREFERRED_ISSUER = 0x00000100,
        CERT_TRUST_HAS_ISSUANCE_CHAIN_POLICY = 0x00000200,
        CERT_TRUST_HAS_VALID_NAME_CONSTRAINTS = 0x00000400,

        // These can be applied to chains only
        CERT_TRUST_IS_COMPLEX_CHAIN = 0x00010000,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CERT_TRUST_STATUS
    {
        public CertTrustErrorStatus dwErrorStatus;
        public CertTrustInfoStatus dwInfoStatus;
    }

    // DIFFERENT FROM COREFX (ADDITION)
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct CRL_ENTRY
    {
        public CRYPTOAPI_BLOB SerialNumber;
        public FILETIME RevocationDate;
        public int cExtension;
        public IntPtr rgExtension;
    };

    // DIFFERENT FROM COREFX (ADDITION)
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct CRL_INFO
    {
        public int dwVersion;
        public CRYPT_ALGORITHM_IDENTIFIER SignatureAlgorithm;
        public CRYPTOAPI_BLOB Issuer;
        public FILETIME ThisUpdate;
        public FILETIME NextUpdate;
        public int cCRLEntry;
        public CRL_ENTRY* rgCRLEntry;
        public int cExtension;
        public CERT_EXTENSION* rgExtension;
    };

    // DIFFERENT FROM COREFX (ADDITION)
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct CRL_CONTEXT
    {
        public CertEncodingType dwCertEncodingType;
        public byte* pbCrlEncoded;
        public int cbCrlEncoded;
        public CRL_INFO* pCrlInfo;
        public IntPtr hCertStore;
    };

    // DIFFERENT FROM COREFX (ADDITION)
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct CERT_REVOCATION_CRL_INFO
    {
        public int cbSize;
        public CRL_CONTEXT* pBaseCRLContext;
        public CRL_CONTEXT* pDeltaCRLContext;
        public CRL_ENTRY* pCrlEntry;
        public int fDeltaCrlEntry;
    };

    // DIFFERENT FROM COREFX (ADDITION)
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct CERT_REVOCATION_INFO
    {
        public int cbSize;
        public CertTrustErrorStatus dwRevocationResult;
        public IntPtr pszRevocationOid;
        public IntPtr pvOidSpecificInfo;
        public int fHasFreshnessTime;
        public int dwFreshnessTime;
        public CERT_REVOCATION_CRL_INFO* pCrlInfo;
    }

    // DIFFERENT FROM COREFX (MODIFIED)
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct CERT_CHAIN_ELEMENT
    {
        public int cbSize;
        public CERT_CONTEXT* pCertContext;
        public CERT_TRUST_STATUS TrustStatus;
        public CERT_REVOCATION_INFO* pRevocationInfo;
        public IntPtr pIssuanceUsage;
        public IntPtr pApplicationUsage;
        public IntPtr pwszExtendedErrorInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct CERT_SIMPLE_CHAIN
    {
        public int cbSize;
        public CERT_TRUST_STATUS TrustStatus;
        public int cElement;
        public CERT_CHAIN_ELEMENT** rgpElement;
        public IntPtr pTrustListInfo;

        // fHasRevocationFreshnessTime is only set if we are able to retrieve
        // revocation information for all elements checked for revocation.
        // For a CRL its CurrentTime - ThisUpdate.
        //
        // dwRevocationFreshnessTime is the largest time across all elements
        // checked.
        public int fHasRevocationFreshnessTime;
        public int dwRevocationFreshnessTime;    // seconds
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct CERT_CHAIN_CONTEXT
    {
        public int cbSize;
        public CERT_TRUST_STATUS TrustStatus;
        public int cChain;
        public CERT_SIMPLE_CHAIN** rgpChain;

        // Following is returned when CERT_CHAIN_RETURN_LOWER_QUALITY_CONTEXTS
        // is set in dwFlags
        public int cLowerQualityChainContext;
        public CERT_CHAIN_CONTEXT** rgpLowerQualityChainContext;

        // fHasRevocationFreshnessTime is only set if we are able to retrieve
        // revocation information for all elements checked for revocation.
        // For a CRL its CurrentTime - ThisUpdate.
        //
        // dwRevocationFreshnessTime is the largest time across all elements
        // checked.
        public int fHasRevocationFreshnessTime;
        public int dwRevocationFreshnessTime;    // seconds

        // Flags passed when created via CertGetCertificateChain
        public int dwCreateFlags;

        // Following is updated with unique Id when the chain context is logged.
        public Guid ChainId;
    }
}