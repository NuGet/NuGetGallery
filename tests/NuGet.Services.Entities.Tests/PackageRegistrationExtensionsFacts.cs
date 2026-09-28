// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
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

        public class TheDurableIdentityValueRules
        {
            private const string Div = "1.3.6.1.4.1.311.97.990309390.766961637.194916062.941502583";
            private readonly PackageRegistration _packageRegistration;
            private readonly User _owner;
            private readonly User _otherOwner;

            public TheDurableIdentityValueRules()
            {
                _owner = new User { Key = 1, Username = "a" };
                _otherOwner = new User { Key = 2, Username = "b" };
                _packageRegistration = new PackageRegistration { Key = 3, Id = "c" };
                _packageRegistration.Owners.Add(_owner);
                _packageRegistration.Owners.Add(_otherOwner);
            }

            [Fact]
            public void IsSigningAllowed_WhenOwnerHasOnlyDurableIdentityValue_ReturnsTrue()
            {
                LinkDurableIdentityValue(_owner, Div);

                Assert.True(_packageRegistration.IsSigningAllowed());
            }

            [Fact]
            public void IsSigningRequired_WhenAllOwnersHaveOnlyDurableIdentityValues_ReturnsTrue()
            {
                LinkDurableIdentityValue(_owner, Div);
                LinkDurableIdentityValue(_otherOwner, Div);

                Assert.True(_packageRegistration.IsSigningRequired());
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WhenOwnerHasMatchingDurableIdentityValue_ReturnsTrue()
            {
                LinkDurableIdentityValue(_otherOwner, Div);

                Assert.True(_packageRegistration.IsAcceptableSigningCertificate("unknown-thumbprint", Div));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WhenDurableIdentityValueIsNull_FallsBackToThumbprint()
            {
                LinkDurableIdentityValue(_owner, Div);

                Assert.False(_packageRegistration.IsAcceptableSigningCertificate("unknown-thumbprint", durableIdentityValue: null));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WhenDurableIdentityValueDiffers_ReturnsFalse()
            {
                LinkDurableIdentityValue(_owner, Div);

                Assert.False(_packageRegistration.IsAcceptableSigningCertificate("unknown-thumbprint", Div + "1"));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WhenRequiredSignerLacksDurableIdentityValue_ReturnsFalse()
            {
                LinkDurableIdentityValue(_owner, Div);
                _packageRegistration.RequiredSigners.Add(_otherOwner);

                Assert.False(_packageRegistration.IsAcceptableSigningCertificate("unknown-thumbprint", Div));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WhenRequiredSignerHasDurableIdentityValue_ReturnsTrue()
            {
                LinkDurableIdentityValue(_otherOwner, Div);
                _packageRegistration.RequiredSigners.Add(_otherOwner);

                Assert.True(_packageRegistration.IsAcceptableSigningCertificate("unknown-thumbprint", Div));
            }

            [Fact]
            public void GetSigningAccounts_WhenNoRequiredSigner_ReturnsAllOwners()
            {
                var accounts = _packageRegistration.GetSigningAccounts();

                Assert.Equal(new[] { _owner, _otherOwner }, accounts.OrderBy(a => a.Key));
            }

            [Fact]
            public void GetSigningAccounts_WhenRequiredSigner_ReturnsOnlyRequiredSigner()
            {
                _packageRegistration.RequiredSigners.Add(_otherOwner);

                var accounts = _packageRegistration.GetSigningAccounts();

                Assert.Equal(new[] { _otherOwner }, accounts);
            }

            private static void LinkDurableIdentityValue(User user, string value)
            {
                var durableIdentityValue = new DurableIdentityValue { Key = 10, Value = value };
                var link = new UserDurableIdentityValue
                {
                    Key = user.UserDurableIdentityValues.Count + 1,
                    DurableIdentityValue = durableIdentityValue,
                    DurableIdentityValueKey = durableIdentityValue.Key,
                    User = user,
                    UserKey = user.Key,
                };
                user.UserDurableIdentityValues.Add(link);
                durableIdentityValue.UserDurableIdentityValues.Add(link);
            }
        }
    }
}