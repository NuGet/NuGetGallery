// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Services.Entities.Tests
{
    public class PackageRegistrationExtensionsFacts
    {
        public class TheIsSigningAllowedMethod
        {
            private readonly Package _package;
            private readonly PackageRegistration _packageRegistration;
            private readonly User _user;

            public TheIsSigningAllowedMethod()
            {
                _user = new User()
                {
                    Key = 1,
                    Username = "a"
                };
                _packageRegistration = new PackageRegistration()
                {
                    Key = 2,
                    Id = "b"
                };
                _package = new Package()
                {
                    Key = 3,
                    PackageRegistration = _packageRegistration
                };

                _packageRegistration.Owners.Add(_user);
            }

            [Fact]
            public void IsSigningAllowed_WhenPackageRegistrationIsNull_Throws()
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => PackageRegistrationExtensions.IsSigningRequired(packageRegistration: null));

                Assert.Equal("packageRegistration", exception.ParamName);
            }

            [Fact]
            public void IsSigningAllowed_WithOneOwner_WhenRequiredSignerIsNullAndOwnerHasNoCertificate_ReturnsFalse()
            {
                Assert.False(_packageRegistration.IsSigningAllowed());
            }

            [Fact]
            public void IsSigningAllowed_WithOneOwner_WhenRequiredSignerIsNullAndOwnerHasCertificate_ReturnsTrue()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 4,
                    User = _user,
                    UserKey = _user.Key
                });

                Assert.True(_packageRegistration.IsSigningAllowed());
            }

            [Fact]
            public void IsSigningAllowed_WithTwoOwners_WhenRequiredSignerIsNullAndOnlyOwnerHasCertificate_ReturnsTrue()
            {
                var otherOwner = new User()
                {
                    Key = 4,
                    Username = "d"
                };

                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key
                });

                _packageRegistration.Owners.Add(otherOwner);

                Assert.True(_packageRegistration.IsSigningAllowed());
            }

            [Fact]
            public void IsSigningAllowed_WithTwoOwners_WhenRequiredSignerIsNullAndOnlyOtherOwnerHasCertificate_ReturnsTrue()
            {
                var otherOwner = new User()
                {
                    Key = 4,
                    Username = "d"
                };
                otherOwner.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = otherOwner,
                    UserKey = otherOwner.Key
                });

                _packageRegistration.Owners.Add(otherOwner);

                Assert.True(_packageRegistration.IsSigningAllowed());
            }

            [Fact]
            public void IsSigningAllowed_WithOneOwner_WhenRequiredSignerIsOwnerAndOwnerHasNoCertificate_ReturnsFalse()
            {
                _packageRegistration.RequiredSigners.Add(_user);

                Assert.False(_packageRegistration.IsSigningAllowed());
            }

            [Fact]
            public void IsSigningAllowed_WithOneOwner_WhenRequiredSignerIsOwnerAndOwnerHasCertificate_ReturnsTrue()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 4,
                    User = _user,
                    UserKey = _user.Key
                });

                _packageRegistration.RequiredSigners.Add(_user);

                Assert.True(_packageRegistration.IsSigningAllowed());
            }

            [Fact]
            public void IsSigningAllowed_WithTwoOwners_WhenRequiredSignerIsOwnerAndOnlyOwnerHasCertificate_ReturnsTrue()
            {
                var otherOwner = new User()
                {
                    Key = 4,
                    Username = "d"
                };
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key
                });

                _packageRegistration.Owners.Add(otherOwner);
                _packageRegistration.RequiredSigners.Add(_user);

                Assert.True(_packageRegistration.IsSigningAllowed());
            }

            [Fact]
            public void IsSigningAllowed_WithTwoOwners_WhenRequiredSignerIsOwnerAndOnlyOtherOwnerHasCertificate_ReturnsFalse()
            {
                var otherOwner = new User()
                {
                    Key = 4,
                    Username = "d"
                };
                otherOwner.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = otherOwner,
                    UserKey = otherOwner.Key
                });

                _packageRegistration.Owners.Add(otherOwner);
                _packageRegistration.RequiredSigners.Add(_user);

                Assert.False(_packageRegistration.IsSigningAllowed());
            }

            [Fact]
            public void IsSigningAllowed_WithTwoOwners_WhenRequiredSignerIsOwnerAndAllOwnersHaveCertificate_ReturnsTrue()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key
                });
                var otherOwner = new User()
                {
                    Key = 4,
                    Username = "d"
                };
                otherOwner.UserCertificates.Add(new UserCertificate()
                {
                    Key = 6,
                    User = otherOwner,
                    UserKey = otherOwner.Key
                });

                _packageRegistration.Owners.Add(otherOwner);
                _packageRegistration.RequiredSigners.Add(_user);

                Assert.True(_packageRegistration.IsSigningAllowed());
            }

            [Fact]
            public void IsSigningAllowed_WithTwoOwners_WhenRequiredSignerIsOwnerAndNoOwnersHaveCertificate_ReturnsFalse()
            {
                var otherOwner = new User()
                {
                    Key = 4,
                    Username = "d"
                };

                _packageRegistration.Owners.Add(otherOwner);
                _packageRegistration.RequiredSigners.Add(_user);

                Assert.False(_packageRegistration.IsSigningAllowed());
            }

            [Fact]
            public void IsSigningAllowed_WithTwoOwners_WhenRequiredSignerIsNullAndNeitherOwnerHasCertificate_ReturnsFalse()
            {
                var otherOwner = new User()
                {
                    Key = 4,
                    Username = "d"
                };

                _packageRegistration.Owners.Add(otherOwner);

                Assert.False(_packageRegistration.IsSigningAllowed());
            }
        }

        public class TheIsSigningRequiredMethod
        {
            private readonly Package _package;
            private readonly PackageRegistration _packageRegistration;
            private readonly User _user;

            public TheIsSigningRequiredMethod()
            {
                _user = new User()
                {
                    Key = 1,
                    Username = "a"
                };
                _packageRegistration = new PackageRegistration()
                {
                    Key = 2,
                    Id = "b"
                };
                _package = new Package()
                {
                    Key = 3,
                    PackageRegistration = _packageRegistration
                };

                _packageRegistration.Owners.Add(_user);
            }

            [Fact]
            public void IsSigningRequired_WhenPackageRegistrationIsNull_Throws()
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => PackageRegistrationExtensions.IsSigningRequired(packageRegistration: null));

                Assert.Equal("packageRegistration", exception.ParamName);
            }

            [Fact]
            public void IsSigningRequired_WithOneOwner_WhenRequiredSignerIsNullAndOwnerHasNoCertificate_ReturnsFalse()
            {
                Assert.False(_packageRegistration.IsSigningRequired());
            }

            [Fact]
            public void IsSigningRequired_WithOneOwner_WhenRequiredSignerIsNullAndOwnerHasCertificate_ReturnsTrue()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 4,
                    User = _user,
                    UserKey = _user.Key
                });

                Assert.True(_packageRegistration.IsSigningRequired());
            }

            [Fact]
            public void IsSigningRequired_WithTwoOwners_WhenRequiredSignerIsNullAndOnlyOwnerHasCertificate_ReturnsFalse()
            {
                var otherOwner = new User()
                {
                    Key = 4,
                    Username = "d"
                };

                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key
                });

                _packageRegistration.Owners.Add(otherOwner);

                Assert.False(_packageRegistration.IsSigningRequired());
            }

            [Fact]
            public void IsSigningRequired_WithTwoOwners_WhenRequiredSignerIsNullAndOnlyOtherOwnerHasCertificate_ReturnsFalse()
            {
                var otherOwner = new User()
                {
                    Key = 4,
                    Username = "d"
                };
                otherOwner.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = otherOwner,
                    UserKey = otherOwner.Key
                });

                _packageRegistration.Owners.Add(otherOwner);

                Assert.False(_packageRegistration.IsSigningRequired());
            }

            [Fact]
            public void IsSigningRequired_WithOneOwner_WhenRequiredSignerIsOwnerAndOwnerHasNoCertificate_ReturnsFalse()
            {
                _packageRegistration.RequiredSigners.Add(_user);

                Assert.False(_packageRegistration.IsSigningRequired());
            }

            [Fact]
            public void IsSigningRequired_WithOneOwner_WhenRequiredSignerIsOwnerAndOwnerHasCertificate_ReturnsTrue()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 4,
                    User = _user,
                    UserKey = _user.Key
                });

                _packageRegistration.RequiredSigners.Add(_user);

                Assert.True(_packageRegistration.IsSigningRequired());
            }

            [Fact]
            public void IsSigningRequired_WithTwoOwners_WhenRequiredSignerIsOwnerAndOnlyOwnerHasCertificate_ReturnsTrue()
            {
                var otherOwner = new User()
                {
                    Key = 4,
                    Username = "d"
                };
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key
                });

                _packageRegistration.Owners.Add(otherOwner);
                _packageRegistration.RequiredSigners.Add(_user);

                Assert.True(_packageRegistration.IsSigningRequired());
            }

            [Fact]
            public void IsSigningRequired_WithTwoOwners_WhenRequiredSignerIsOwnerAndOnlyOtherOwnerHasCertificate_ReturnsFalse()
            {
                var otherOwner = new User()
                {
                    Key = 4,
                    Username = "d"
                };
                otherOwner.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = otherOwner,
                    UserKey = otherOwner.Key
                });

                _packageRegistration.Owners.Add(otherOwner);
                _packageRegistration.RequiredSigners.Add(_user);

                Assert.False(_packageRegistration.IsSigningRequired());
            }

            [Fact]
            public void IsSigningRequired_WithTwoOwners_WhenRequiredSignerIsOwnerAndAllOwnersHaveCertificate_ReturnsTrue()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key
                });
                var otherOwner = new User()
                {
                    Key = 4,
                    Username = "d"
                };
                otherOwner.UserCertificates.Add(new UserCertificate()
                {
                    Key = 6,
                    User = otherOwner,
                    UserKey = otherOwner.Key
                });

                _packageRegistration.Owners.Add(otherOwner);
                _packageRegistration.RequiredSigners.Add(_user);

                Assert.True(_packageRegistration.IsSigningRequired());
            }
        }

        public class TheIsAcceptableSigningCertificateMethod
        {
            private readonly Package _package;
            private readonly PackageRegistration _packageRegistration;
            private readonly User _user;
            private readonly Certificate _certificate;

            public TheIsAcceptableSigningCertificateMethod()
            {
                _user = new User()
                {
                    Key = 1,
                    Username = "a"
                };
                _packageRegistration = new PackageRegistration()
                {
                    Key = 2,
                    Id = "b"
                };
                _package = new Package()
                {
                    Key = 3,
                    PackageRegistration = _packageRegistration
                };
                _certificate = new Certificate()
                {
                    Key = 4,
                    Thumbprint = "c"
                };

                _packageRegistration.Owners.Add(_user);
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WhenPackageRegistrationIsNull_Throws()
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => PackageRegistrationExtensions.IsAcceptableSigningCertificate(packageRegistration: null, thumbprint: "a"));

                Assert.Equal("packageRegistration", exception.ParamName);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData(" ")]
            public void IsAcceptableSigningCertificate_WhenThumbprintIsInvalid_Throws(string thumbprint)
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => _packageRegistration.IsAcceptableSigningCertificate(thumbprint));

                Assert.Equal("thumbprint", exception.ParamName);
                Assert.StartsWith("The argument cannot be null or empty.", exception.Message);
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithDifferentlyCasedThumbprint_ReturnsTrue()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                Assert.True(_packageRegistration.IsAcceptableSigningCertificate(_certificate.Thumbprint.ToUpperInvariant()));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithOneOwner_WhenRequiredSignerIsNullAndOwnerHasNoCertificate_ReturnsFalse()
            {
                Assert.False(_packageRegistration.IsAcceptableSigningCertificate(_certificate.Thumbprint));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithOneOwner_WhenRequiredSignerIsNullAndOwnerHasNonMatchingCertificate_ReturnsFalse()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                Assert.False(_packageRegistration.IsAcceptableSigningCertificate(thumbprint: "nonmatching"));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithOneOwner_WhenRequiredSignerIsNullAndOwnerHasMatchingCertificate_ReturnsTrue()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                Assert.True(_packageRegistration.IsAcceptableSigningCertificate(_certificate.Thumbprint));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithTwoOwners_WhenRequiredSignerIsNullAndOnlyOwnerHasNonMatchingCertificate_ReturnsFalse()
            {
                var otherOwner = new User()
                {
                    Key = 5,
                    Username = "d"
                };

                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 6,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.Owners.Add(otherOwner);

                Assert.False(_packageRegistration.IsAcceptableSigningCertificate(thumbprint: "nonmatching"));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithTwoOwners_WhenRequiredSignerIsNullAndOnlyOwnerHasMatchingCertificate_ReturnsTrue()
            {
                var otherOwner = new User()
                {
                    Key = 5,
                    Username = "d"
                };

                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.Owners.Add(otherOwner);

                Assert.True(_packageRegistration.IsAcceptableSigningCertificate(_certificate.Thumbprint));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithTwoOwners_WhenRequiredSignerIsNullAndOnlyOtherOwnerHasNonMatchingCertificate_ReturnsFalse()
            {
                var otherOwner = new User()
                {
                    Key = 5,
                    Username = "d"
                };
                otherOwner.UserCertificates.Add(new UserCertificate()
                {
                    Key = 6,
                    User = otherOwner,
                    UserKey = otherOwner.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.Owners.Add(otherOwner);

                Assert.False(_packageRegistration.IsAcceptableSigningCertificate(thumbprint: "nonmatching"));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithTwoOwners_WhenRequiredSignerIsNullAndOnlyOtherOwnerHasMatchingCertificate_ReturnsTrue()
            {
                var otherOwner = new User()
                {
                    Key = 5,
                    Username = "d"
                };
                otherOwner.UserCertificates.Add(new UserCertificate()
                {
                    Key = 6,
                    User = otherOwner,
                    UserKey = otherOwner.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.Owners.Add(otherOwner);

                Assert.True(_packageRegistration.IsAcceptableSigningCertificate(_certificate.Thumbprint));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithOneOwner_WhenRequiredSignerIsOwnerAndOwnerHasNoCertificate_ReturnsFalse()
            {
                _packageRegistration.RequiredSigners.Add(_user);

                Assert.False(_packageRegistration.IsAcceptableSigningCertificate(_certificate.Thumbprint));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithOneOwner_WhenRequiredSignerIsOwnerAndOwnerHasNonMatchingCertificate_ReturnsFalse()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.RequiredSigners.Add(_user);

                Assert.False(_packageRegistration.IsAcceptableSigningCertificate(thumbprint: "nonmatching"));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithOneOwner_WhenRequiredSignerIsOwnerAndOwnerHasMatchingCertificate_ReturnsTrue()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.RequiredSigners.Add(_user);

                Assert.True(_packageRegistration.IsAcceptableSigningCertificate(_certificate.Thumbprint));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithTwoOwners_WhenRequiredSignerIsOwnerAndOnlyOwnerHasNonMatchingCertificate_ReturnsFalse()
            {
                var otherOwner = new User()
                {
                    Key = 5,
                    Username = "d"
                };
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 6,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.Owners.Add(otherOwner);
                _packageRegistration.RequiredSigners.Add(_user);

                Assert.False(_packageRegistration.IsAcceptableSigningCertificate(thumbprint: "nonmatching"));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithTwoOwners_WhenRequiredSignerIsOwnerAndOnlyOwnerHasMatchingCertificate_ReturnsTrue()
            {
                var otherOwner = new User()
                {
                    Key = 5,
                    Username = "d"
                };
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 6,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.Owners.Add(otherOwner);
                _packageRegistration.RequiredSigners.Add(_user);

                Assert.True(_packageRegistration.IsAcceptableSigningCertificate(_certificate.Thumbprint));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithTwoOwners_WhenRequiredSignerIsOwnerAndOnlyOtherOwnerHasNonMatchingCertificate_ReturnsFalse()
            {
                var otherOwner = new User()
                {
                    Key = 5,
                    Username = "d"
                };
                otherOwner.UserCertificates.Add(new UserCertificate()
                {
                    Key = 6,
                    User = otherOwner,
                    UserKey = otherOwner.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.Owners.Add(otherOwner);
                _packageRegistration.RequiredSigners.Add(_user);

                Assert.False(_packageRegistration.IsAcceptableSigningCertificate(thumbprint: "nonmatching"));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithTwoOwners_WhenRequiredSignerIsOwnerAndOnlyOtherOwnerHasMatchingCertificate_ReturnsFalse()
            {
                var otherOwner = new User()
                {
                    Key = 5,
                    Username = "d"
                };
                otherOwner.UserCertificates.Add(new UserCertificate()
                {
                    Key = 6,
                    User = otherOwner,
                    UserKey = otherOwner.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.Owners.Add(otherOwner);
                _packageRegistration.RequiredSigners.Add(_user);

                Assert.False(_packageRegistration.IsAcceptableSigningCertificate(_certificate.Thumbprint));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithTwoOwners_WhenRequiredSignerIsOwnerAndAllOwnersHaveNonMatchingCertificate_ReturnsFalse()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });
                var otherOwner = new User()
                {
                    Key = 6,
                    Username = "d"
                };
                otherOwner.UserCertificates.Add(new UserCertificate()
                {
                    Key = 7,
                    User = otherOwner,
                    UserKey = otherOwner.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.Owners.Add(otherOwner);
                _packageRegistration.RequiredSigners.Add(_user);

                Assert.False(_packageRegistration.IsAcceptableSigningCertificate(thumbprint: "nonmatching"));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithTwoOwners_WhenRequiredSignerIsOwnerAndAllOwnersHaveMatchingCertificate_ReturnsTrue()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });
                var otherOwner = new User()
                {
                    Key = 6,
                    Username = "d"
                };
                otherOwner.UserCertificates.Add(new UserCertificate()
                {
                    Key = 7,
                    User = otherOwner,
                    UserKey = otherOwner.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.Owners.Add(otherOwner);
                _packageRegistration.RequiredSigners.Add(_user);

                Assert.True(_packageRegistration.IsAcceptableSigningCertificate(_certificate.Thumbprint));
            }
        }
    }
}