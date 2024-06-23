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
            public void IsSigningAllowed_WithOneOwner_WhenRequiredSignerIsNullAndOwnerHasCertificatePattern_ReturnsTrue()
            {
                _user.UserCertificatePatterns.Add(new UserCertificatePattern
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
            public void IsSigningRequired_WithOneOwner_WhenRequiredSignerIsNullAndOwnerHasCertificatePattern_ReturnsTrue()
            {
                _user.UserCertificatePatterns.Add(new UserCertificatePattern
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
    }
}