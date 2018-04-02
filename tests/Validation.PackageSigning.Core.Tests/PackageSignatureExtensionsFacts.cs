// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Validation;
using Xunit;

namespace Validation.PackageSigning.Core.Tests
{
    public class PackageSignatureExtensionsFacts
    {
        private const int PackageKey = 1001;
        private const string PackageId = "NuGet.Versioning";
        private const string PackageNormalizedVersion = "4.3.0.0-ALPHA+git";
        private static readonly Guid ValidationId = new Guid("fb9c0bac-3d4d-4cc7-ac2d-b3940e15b94d");

        public class TheIsPromotableMethod
        {
            [Fact]
            public void ThrowsIfSignatureIsInvalid()
            {
                var signature = new PackageSignature
                {
                    Status = PackageSignatureStatus.Invalid
                };

                Assert.Throws<ArgumentException>("signature", () => signature.IsPromotable());
            }

            public static IEnumerable<object[]> ValidSignaturesArePromotableData()
            {
                var cert1SecondAgo = new EndCertificate
                {
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = DateTime.UtcNow.AddSeconds(-1),
                };

                var certRevoked1SecondAgo = new EndCertificate
                {
                    Status = EndCertificateStatus.Revoked,
                    StatusUpdateTime = DateTime.UtcNow.AddSeconds(-1),
                    RevocationTime = DateTime.UtcNow.AddSeconds(-1),
                };

                var cert1YearAgo = new EndCertificate
                {
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = DateTime.UtcNow.AddYears(-1),
                };

                // A signature whose timestamp is BEFORE the signature's and timestamps' certificates
                // last updates should be promotable.
                yield return new object[]
                {
                    true,
                    new PackageSignature
                    {
                        EndCertificate = cert1SecondAgo,
                        TrustedTimestamps = new[]
                        {
                            new TrustedTimestamp
                            {
                                Value = DateTime.UtcNow.AddDays(-1),
                                EndCertificate = cert1SecondAgo,
                            }
                        },
                    },
                };

                // A signature whose timestamp is AFTER the signature's certificate last update should not
                // not be promotable
                yield return new object[]
                {
                    false,
                    new PackageSignature
                    {
                        EndCertificate = cert1YearAgo,
                        TrustedTimestamps = new[]
                        {
                            new TrustedTimestamp
                            {
                                Value = DateTime.UtcNow.AddDays(-1),
                                EndCertificate = cert1SecondAgo,
                            }
                        },
                    },
                };

                // A signature whose timestamp is AFTER the timestamp's certificate last update should not
                // be promotable
                yield return new object[]
                {
                    false,
                    new PackageSignature
                    {
                        EndCertificate = cert1SecondAgo,
                        TrustedTimestamps = new[]
                        {
                            new TrustedTimestamp
                            {
                                Value = DateTime.UtcNow.AddDays(-1),
                                EndCertificate = cert1YearAgo,
                            }
                        },
                    },
                };

                // The latest timestamp should be used for promotion decisions.
                yield return new object[]
                {
                    false,
                    new PackageSignature
                    {
                        EndCertificate = cert1YearAgo,
                        TrustedTimestamps = new[]
                        {
                            new TrustedTimestamp
                            {
                                Value = DateTime.UtcNow.AddDays(-1),
                                EndCertificate = cert1YearAgo,
                            },
                            new TrustedTimestamp
                            {
                                Value = DateTime.UtcNow.AddYears(-10),
                                EndCertificate = cert1YearAgo,
                            }
                        },
                    },
                };

                // A signature whose signing certificate is revoked should be promoted to "Valid" as long as the revocation
                // time begins after the package was signed.
                yield return new object[]
                {
                    true,
                    new PackageSignature
                    {
                        EndCertificate = certRevoked1SecondAgo,
                        TrustedTimestamps = new[]
                        {
                            new TrustedTimestamp
                            {
                                Value = DateTime.UtcNow.AddDays(-1),
                                EndCertificate = cert1SecondAgo,
                            }
                        },
                    },
                };
            }

            [Theory]
            [MemberData(nameof(ValidSignaturesArePromotableData))]
            public void ValidSignaturesArePromotable(bool expectedIsPromotable, PackageSignature signature)
            {
                signature.PackageKey = PackageKey;
                signature.Status = PackageSignatureStatus.Unknown;

                // Act & Assert
                if (expectedIsPromotable)
                {
                    Assert.True(signature.IsPromotable());
                }
                else
                {
                    Assert.False(signature.IsPromotable());
                }
            }
        }
    }
}
