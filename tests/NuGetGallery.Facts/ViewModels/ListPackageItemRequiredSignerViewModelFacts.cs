// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Security;
using Xunit;

namespace NuGetGallery.ViewModels
{
    public class ListPackageItemRequiredSignerViewModelFacts
    {
        private readonly User _currentUser;
        private readonly User _otherUser;
        private readonly Mock<ISecurityPolicyService> _securityPolicyService;

        public ListPackageItemRequiredSignerViewModelFacts()
        {
            _currentUser = new User()
            {
                Key = 1,
                Username = "a"
            };

            _otherUser = new User()
            {
                Key = 2,
                Username = "b"
            };

            _securityPolicyService = new Mock<ISecurityPolicyService>(MockBehavior.Strict);
        }

        [Fact]
        public void Constructor_WhenPackageIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new ListPackageItemRequiredSignerViewModel(
                    package: null,
                    currentUser: _currentUser,
                    securityPolicyService: _securityPolicyService.Object,
                    wasAADLoginOrMultiFactorAuthenticated: true,
                    overrideIconUrl: null));

            Assert.Equal("package", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenCurrentUserIsNull_Throws()
        {
            var package = new Package()
            {
                PackageRegistration = new PackageRegistration(),
                Version = "1.0.0"
            };

            var exception = Assert.Throws<ArgumentNullException>(
                () => new ListPackageItemRequiredSignerViewModel(
                    package,
                    currentUser: null,
                    securityPolicyService: _securityPolicyService.Object,
                    wasAADLoginOrMultiFactorAuthenticated: true,
                    overrideIconUrl: null));

            Assert.Equal("currentUser", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenSecurityPolicyServiceIsNull_Throws()
        {
            var package = new Package()
            {
                PackageRegistration = new PackageRegistration(),
                Version = "1.0.0"
            };

            var exception = Assert.Throws<ArgumentNullException>(
                () => new ListPackageItemRequiredSignerViewModel(
                    package,
                    _currentUser,
                    securityPolicyService: null,
                    wasAADLoginOrMultiFactorAuthenticated: true,
                    overrideIconUrl: null));

            Assert.Equal("securityPolicyService", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenPackageHasOneOwnerAndItIsTheCurrentUser_WhenRequiredSignerIsNull()
        {
            var package = new Package()
            {
                PackageRegistration = new PackageRegistration()
                {
                    Owners = new List<User>() { _currentUser }
                },
                Version = "1.0.0"
            };

            _securityPolicyService.Setup(x => x.IsSubscribed(
                    It.Is<User>(user => user == _currentUser),
                    It.Is<string>(policyName => policyName == ControlRequiredSignerPolicy.PolicyName)))
                .Returns(false);

            var viewModel = new ListPackageItemRequiredSignerViewModel(
                package,
                _currentUser,
                _securityPolicyService.Object,
                wasAADLoginOrMultiFactorAuthenticated: true,
                overrideIconUrl: null);

            Assert.Equal(_currentUser.Username, viewModel.RequiredSigner.Username);
            Assert.Equal($"{_currentUser.Username} (0 certificates)", viewModel.RequiredSigner.DisplayText);
            Assert.Null(viewModel.RequiredSignerMessage);
            Assert.Empty(viewModel.AllSigners);
            Assert.True(viewModel.ShowRequiredSigner);
            Assert.True(viewModel.ShowTextBox);
            Assert.False(viewModel.CanEditRequiredSigner);

            _securityPolicyService.VerifyAll();
        }

        [Fact]
        public void Constructor_WhenPackageHasOneOwnerAndItIsTheCurrentUser_WhenRequiredSignerIsCurrentUser()
        {
            var package = new Package()
            {
                PackageRegistration = new PackageRegistration()
                {
                    Owners = new List<User>() { _currentUser },
                    RequiredSigners = new HashSet<User>() { _currentUser }
                },
                Version = "1.0.0"
            };

            _securityPolicyService.Setup(x => x.IsSubscribed(
                    It.Is<User>(user => user == _currentUser),
                    It.Is<string>(policyName => policyName == ControlRequiredSignerPolicy.PolicyName)))
                .Returns(false);

            var viewModel = new ListPackageItemRequiredSignerViewModel(
                package,
                _currentUser,
                _securityPolicyService.Object,
                wasAADLoginOrMultiFactorAuthenticated: true,
                overrideIconUrl: null);

            Assert.Equal(_currentUser.Username, viewModel.RequiredSigner.Username);
            Assert.Equal($"{_currentUser.Username} (0 certificates)", viewModel.RequiredSigner.DisplayText);
            Assert.Null(viewModel.RequiredSignerMessage);
            Assert.Empty(viewModel.AllSigners);
            Assert.True(viewModel.ShowRequiredSigner);
            Assert.True(viewModel.ShowTextBox);
            Assert.False(viewModel.CanEditRequiredSigner);

            _securityPolicyService.VerifyAll();
        }

        [Fact]
        public void Constructor_WhenPackageHasOneOwnerAndItIsTheCurrentUser_WhenRequiredSignerIsAnotherUser()
        {
            var package = new Package()
            {
                PackageRegistration = new PackageRegistration()
                {
                    Owners = new List<User>() { _currentUser },
                    RequiredSigners = new HashSet<User>() { _otherUser }
                },
                Version = "1.0.0"
            };

            _securityPolicyService.Setup(x => x.IsSubscribed(
                    It.Is<User>(user => user == _currentUser),
                    It.Is<string>(policyName => policyName == ControlRequiredSignerPolicy.PolicyName)))
                .Returns(false);

            var viewModel = new ListPackageItemRequiredSignerViewModel(
                package,
                _currentUser,
                _securityPolicyService.Object,
                wasAADLoginOrMultiFactorAuthenticated: true,
                overrideIconUrl: null);

            Assert.Equal(_otherUser.Username, viewModel.RequiredSigner.Username);
            Assert.Equal($"{_otherUser.Username} (0 certificates)", viewModel.RequiredSigner.DisplayText);
            Assert.Null(viewModel.RequiredSignerMessage);
            VerifySigners(new[] { _otherUser, _currentUser }, viewModel.AllSigners, expectAnySigner: false);
            Assert.True(viewModel.ShowRequiredSigner);
            Assert.False(viewModel.ShowTextBox);
            Assert.True(viewModel.CanEditRequiredSigner);

            _securityPolicyService.VerifyAll();
        }

        [Fact]
        public void Constructor_WhenPackageHasTwoOwnersAndTheCurrentUserIsAnOwner_WhenRequiredSignerIsNull()
        {
            var package = new Package()
            {
                PackageRegistration = new PackageRegistration()
                {
                    Owners = new List<User>() { _currentUser, _otherUser }
                },
                Version = "1.0.0"
            };

            _securityPolicyService.Setup(
                x => x.IsSubscribed(
                    It.IsNotNull<User>(),
                    It.Is<string>(s => s == ControlRequiredSignerPolicy.PolicyName)))
                .Returns(false);

            var viewModel = new ListPackageItemRequiredSignerViewModel(
                package,
                _currentUser,
                _securityPolicyService.Object,
                wasAADLoginOrMultiFactorAuthenticated: true,
                overrideIconUrl: null);

            Assert.Equal(string.Empty, viewModel.RequiredSigner.Username);
            Assert.Equal("Any", viewModel.RequiredSigner.DisplayText);
            Assert.Null(viewModel.RequiredSignerMessage);
            VerifySigners(package.PackageRegistration.Owners, viewModel.AllSigners, expectAnySigner: true);
            Assert.True(viewModel.ShowRequiredSigner);
            Assert.False(viewModel.ShowTextBox);
            Assert.True(viewModel.CanEditRequiredSigner);

            _securityPolicyService.VerifyAll();
        }

        [Fact]
        public void Constructor_WhenPackageHasTwoOwnersAndTheCurrentUserIsAnOwnerAndNotMultiFactorAuthenticated_WhenRequiredSignerIsNull()
        {
            var package = new Package()
            {
                PackageRegistration = new PackageRegistration()
                {
                    Owners = new List<User>() { _currentUser, _otherUser }
                },
                Version = "1.0.0"
            };

            _securityPolicyService.Setup(
                x => x.IsSubscribed(
                    It.IsNotNull<User>(),
                    It.Is<string>(s => s == ControlRequiredSignerPolicy.PolicyName)))
                .Returns(false);

            var viewModel = new ListPackageItemRequiredSignerViewModel(
                package,
                _currentUser,
                _securityPolicyService.Object,
                wasAADLoginOrMultiFactorAuthenticated: false,
                overrideIconUrl: null);

            Assert.Equal(string.Empty, viewModel.RequiredSigner.Username);
            Assert.Equal("Any", viewModel.RequiredSigner.DisplayText);
            Assert.Null(viewModel.RequiredSignerMessage);
            VerifySigners(package.PackageRegistration.Owners, viewModel.AllSigners, expectAnySigner: true);
            Assert.True(viewModel.ShowRequiredSigner);
            Assert.False(viewModel.ShowTextBox);
            Assert.False(viewModel.CanEditRequiredSigner);

            _securityPolicyService.VerifyAll();
        }

        [Fact]
        public void Constructor_WhenPackageHasOneOwnerAndTheCurrentUserIsACollaborator_WhenRequiredSignerIsNull()
        {
            var organization = new Organization()
            {
                Key = 7,
                Username = "c"
            };

            organization.Members.Add(new Membership()
            {
                OrganizationKey = organization.Key,
                Organization = organization,
                MemberKey = _currentUser.Key,
                Member = _currentUser,
                IsAdmin = false
            });

            var package = new Package()
            {
                PackageRegistration = new PackageRegistration()
                {
                    Owners = new List<User>() { organization }
                },
                Version = "1.0.0"
            };

            _securityPolicyService.Setup(
                x => x.IsSubscribed(
                    It.IsNotNull<User>(),
                    It.Is<string>(s => s == ControlRequiredSignerPolicy.PolicyName)))
                .Returns(false);

            var viewModel = new ListPackageItemRequiredSignerViewModel(
                package,
                _currentUser,
                _securityPolicyService.Object,
                wasAADLoginOrMultiFactorAuthenticated: true,
                overrideIconUrl: null);

            Assert.Equal("c", viewModel.RequiredSigner.Username);
            Assert.Equal($"{organization.Username} (0 certificates)", viewModel.RequiredSigner.DisplayText);
            Assert.Null(viewModel.RequiredSignerMessage);
            VerifySigners(package.PackageRegistration.Owners, viewModel.AllSigners, expectAnySigner: false);
            Assert.True(viewModel.ShowRequiredSigner);
            Assert.True(viewModel.ShowTextBox);
            Assert.False(viewModel.CanEditRequiredSigner);

            _securityPolicyService.VerifyAll();
        }

        [Fact]
        public void Constructor_WhenPackageHasTwoOwnersAndTheCurrentUserIsAnOwner_WhenRequiredSignerIsCurrentUser()
        {
            var package = new Package()
            {
                PackageRegistration = new PackageRegistration()
                {
                    Owners = new List<User>() { _currentUser, _otherUser },
                    RequiredSigners = new HashSet<User>() { _currentUser }
                },
                Version = "1.0.0"
            };

            _securityPolicyService.Setup(
                x => x.IsSubscribed(
                    It.IsNotNull<User>(),
                    It.Is<string>(s => s == ControlRequiredSignerPolicy.PolicyName)))
                .Returns(false);

            var viewModel = new ListPackageItemRequiredSignerViewModel(
                package,
                _currentUser,
                _securityPolicyService.Object,
                wasAADLoginOrMultiFactorAuthenticated: true,
                overrideIconUrl: null);

            Assert.Equal(_currentUser.Username, viewModel.RequiredSigner.Username);
            Assert.Equal($"{_currentUser.Username} (0 certificates)", viewModel.RequiredSigner.DisplayText);
            Assert.Null(viewModel.RequiredSignerMessage);
            VerifySigners(package.PackageRegistration.Owners, viewModel.AllSigners, expectAnySigner: true);
            Assert.True(viewModel.ShowRequiredSigner);
            Assert.False(viewModel.ShowTextBox);
            Assert.True(viewModel.CanEditRequiredSigner);

            _securityPolicyService.VerifyAll();
        }

        [Fact]
        public void Constructor_WhenPackageHasTwoOwnersAndTheCurrentUserIsAnOwner_WhenRequiredSignerIsAnotherUser()
        {
            var package = new Package()
            {
                PackageRegistration = new PackageRegistration()
                {
                    Owners = new List<User>() { _currentUser, _otherUser },
                    RequiredSigners = new HashSet<User>() { _otherUser }
                },
                Version = "1.0.0"
            };

            _securityPolicyService.Setup(
                x => x.IsSubscribed(
                    It.IsNotNull<User>(),
                    It.Is<string>(s => s == ControlRequiredSignerPolicy.PolicyName)))
                .Returns(false);

            var viewModel = new ListPackageItemRequiredSignerViewModel(
                package,
                _currentUser,
                _securityPolicyService.Object,
                wasAADLoginOrMultiFactorAuthenticated: true,
                overrideIconUrl: null);

            Assert.Equal(_otherUser.Username, viewModel.RequiredSigner.Username);
            Assert.Equal($"{_otherUser.Username} (0 certificates)", viewModel.RequiredSigner.DisplayText);
            Assert.Null(viewModel.RequiredSignerMessage);
            VerifySigners(package.PackageRegistration.Owners, viewModel.AllSigners, expectAnySigner: true);
            Assert.True(viewModel.ShowRequiredSigner);
            Assert.False(viewModel.ShowTextBox);
            Assert.True(viewModel.CanEditRequiredSigner);

            _securityPolicyService.VerifyAll();
        }

        [Fact]
        public void Constructor_WhenPackageHasMultipleOwners_WhenOwnersHaveVaryingCertificateCounts()
        {
            User currentUser = new User()
            {
                Key = 1,
                Username = "a",
                UserCertificates = new List<UserCertificate>()
                {
                    new UserCertificate() { Key = 1 }
                }
            };

            User otherUser1 = new User()
            {
                Key = 2,
                Username = "b"
            };

            User otherUser2 = new User()
            {
                Key = 3,
                Username = "c",
                UserCertificates = new List<UserCertificate>()
                {
                    new UserCertificate() { Key = 2 },
                    new UserCertificate() { Key = 3 }
                }
            };

            var package = new Package()
            {
                PackageRegistration = new PackageRegistration()
                {
                    Owners = new List<User>() { otherUser1, currentUser, otherUser2 },
                    RequiredSigners = new HashSet<User>() { currentUser }
                },
                Version = "1.0.0"
            };

            _securityPolicyService.Setup(
                x => x.IsSubscribed(
                    It.IsNotNull<User>(),
                    It.Is<string>(s => s == ControlRequiredSignerPolicy.PolicyName)))
                .Returns(false);

            var viewModel = new ListPackageItemRequiredSignerViewModel(
                package,
                currentUser,
                _securityPolicyService.Object,
                wasAADLoginOrMultiFactorAuthenticated: true,
                overrideIconUrl: null);

            Assert.Equal(currentUser.Username, viewModel.RequiredSigner.Username);
            Assert.Equal($"{currentUser.Username} (1 certificate)", viewModel.RequiredSigner.DisplayText);
            Assert.Null(viewModel.RequiredSignerMessage);
            VerifySigners(package.PackageRegistration.Owners, viewModel.AllSigners, expectAnySigner: true);
            Assert.True(viewModel.ShowRequiredSigner);
            Assert.False(viewModel.ShowTextBox);
            Assert.True(viewModel.CanEditRequiredSigner);

            _securityPolicyService.Verify();
        }

        [Fact]
        public void Constructor_WhenPackageHasTwoOwnersAndTheCurrentUserIsAnOwner_WhenCurrentUserHasRequiredSignerControl()
        {
            var package = new Package()
            {
                PackageRegistration = new PackageRegistration()
                {
                    Owners = new List<User>() { _currentUser, _otherUser },
                    RequiredSigners = new HashSet<User>() { _currentUser }
                },
                Version = "1.0.0"
            };

            _securityPolicyService.Setup(
                x => x.IsSubscribed(
                    It.Is<User>(u => u == _currentUser),
                    It.Is<string>(s => s == ControlRequiredSignerPolicy.PolicyName)))
                .Returns(true);

            var viewModel = new ListPackageItemRequiredSignerViewModel(
                package,
                _currentUser,
                _securityPolicyService.Object,
                wasAADLoginOrMultiFactorAuthenticated: true,
                overrideIconUrl: null);

            Assert.Equal(_currentUser.Username, viewModel.RequiredSigner.Username);
            Assert.Equal($"{_currentUser.Username} (0 certificates)", viewModel.RequiredSigner.DisplayText);
            Assert.Null(viewModel.RequiredSignerMessage);
            VerifySigners(package.PackageRegistration.Owners, viewModel.AllSigners, expectAnySigner: true);
            Assert.True(viewModel.ShowRequiredSigner);
            Assert.False(viewModel.ShowTextBox);
            Assert.True(viewModel.CanEditRequiredSigner);

            _securityPolicyService.VerifyAll();
        }

        [Fact]
        public void Constructor_WhenPackageHasTwoOwnersAndTheCurrentUserIsAnOwner_WhenCurrentUserDoesNotHaveRequiredSignerControl()
        {
            var package = new Package()
            {
                PackageRegistration = new PackageRegistration()
                {
                    Owners = new List<User>() { _currentUser, _otherUser },
                    RequiredSigners = new HashSet<User>() { _otherUser }
                },
                Version = "1.0.0"
            };

            _securityPolicyService.Setup(
                x => x.IsSubscribed(
                    It.Is<User>(u => u == _currentUser),
                    It.Is<string>(s => s == ControlRequiredSignerPolicy.PolicyName)))
                .Returns(false);
            _securityPolicyService.Setup(
                x => x.IsSubscribed(
                    It.Is<User>(u => u == _otherUser),
                    It.Is<string>(s => s == ControlRequiredSignerPolicy.PolicyName)))
                .Returns(true);

            var viewModel = new ListPackageItemRequiredSignerViewModel(
                package,
                _currentUser,
                _securityPolicyService.Object,
                wasAADLoginOrMultiFactorAuthenticated: true,
                overrideIconUrl: null);

            Assert.Equal(_otherUser.Username, viewModel.RequiredSigner.Username);
            Assert.Equal($"{_otherUser.Username} (0 certificates)", viewModel.RequiredSigner.DisplayText);
            Assert.Equal($"The signing owner is managed by the '{_otherUser.Username}' account.", viewModel.RequiredSignerMessage);
            VerifySigners(new[] { _otherUser }, viewModel.AllSigners, expectAnySigner: false);
            Assert.True(viewModel.ShowRequiredSigner);
            Assert.False(viewModel.ShowTextBox);
            Assert.False(viewModel.CanEditRequiredSigner);

            _securityPolicyService.VerifyAll();
        }

        [Fact]
        public void Constructor_WhenPackageHasThreeOwnersAndTheCurrentUserIsAnOwner_WhenTwoOtherOwnersHaveRequiredSignerControl()
        {
            var otherUser2 = new User()
            {
                Key = 3,
                Username = "c"
            };

            var package = new Package()
            {
                PackageRegistration = new PackageRegistration()
                {
                    Owners = new List<User>() { _currentUser, _otherUser, otherUser2 },
                    RequiredSigners = new HashSet<User>() { _currentUser }
                },
                Version = "1.0.0"
            };

            _securityPolicyService.Setup(
                x => x.IsSubscribed(
                    It.Is<User>(u => u == _currentUser),
                    It.Is<string>(s => s == ControlRequiredSignerPolicy.PolicyName)))
                .Returns(false);
            _securityPolicyService.Setup(
                x => x.IsSubscribed(
                    It.Is<User>(u => u == _otherUser),
                    It.Is<string>(s => s == ControlRequiredSignerPolicy.PolicyName)))
                .Returns(true);
            _securityPolicyService.Setup(
                x => x.IsSubscribed(
                    It.Is<User>(u => u == otherUser2),
                    It.Is<string>(s => s == ControlRequiredSignerPolicy.PolicyName)))
                .Returns(true);

            var viewModel = new ListPackageItemRequiredSignerViewModel(
                package,
                _currentUser,
                _securityPolicyService.Object,
                wasAADLoginOrMultiFactorAuthenticated: true,
                overrideIconUrl: null);

            Assert.Equal(_currentUser.Username, viewModel.RequiredSigner.Username);
            Assert.Equal($"{_currentUser.Username} (0 certificates)", viewModel.RequiredSigner.DisplayText);
            Assert.Equal($"The signing owner is managed by the '{_otherUser.Username}' and '{otherUser2.Username}' accounts.", viewModel.RequiredSignerMessage);
            VerifySigners(new[] { _currentUser }, viewModel.AllSigners, expectAnySigner: false);
            Assert.True(viewModel.ShowRequiredSigner);
            Assert.False(viewModel.ShowTextBox);
            Assert.False(viewModel.CanEditRequiredSigner);

            _securityPolicyService.VerifyAll();
        }

        [Fact]
        public void Constructor_WhenPackageHasFourOwnersAndTheCurrentUserIsAnOwner_WhenThreeOtherOwnersHaveRequiredSignerControl()
        {
            var otherUser2 = new User()
            {
                Key = 3,
                Username = "c"
            };

            var otherUser3 = new User()
            {
                Key = 4,
                Username = "d"
            };

            var package = new Package()
            {
                PackageRegistration = new PackageRegistration()
                {
                    Owners = new List<User>() { _currentUser, _otherUser, otherUser2, otherUser3 },
                    RequiredSigners = new HashSet<User>() { _otherUser }
                },
                Version = "1.0.0"
            };

            _securityPolicyService.Setup(
                x => x.IsSubscribed(
                    It.Is<User>(u => u == _currentUser),
                    It.Is<string>(s => s == ControlRequiredSignerPolicy.PolicyName)))
                .Returns(false);
            _securityPolicyService.Setup(
                x => x.IsSubscribed(
                    It.Is<User>(u => u == _otherUser),
                    It.Is<string>(s => s == ControlRequiredSignerPolicy.PolicyName)))
                .Returns(true);
            _securityPolicyService.Setup(
                x => x.IsSubscribed(
                    It.Is<User>(u => u == otherUser2),
                    It.Is<string>(s => s == ControlRequiredSignerPolicy.PolicyName)))
                .Returns(true);
            _securityPolicyService.Setup(
                x => x.IsSubscribed(
                    It.Is<User>(u => u == otherUser3),
                    It.Is<string>(s => s == ControlRequiredSignerPolicy.PolicyName)))
                .Returns(true);

            var viewModel = new ListPackageItemRequiredSignerViewModel(
                package,
                _currentUser,
                _securityPolicyService.Object,
                wasAADLoginOrMultiFactorAuthenticated: true,
                overrideIconUrl: null);

            Assert.Equal(_otherUser.Username, viewModel.RequiredSigner.Username);
            Assert.Equal($"{_otherUser.Username} (0 certificates)", viewModel.RequiredSigner.DisplayText);
            Assert.Equal($"The signing owner is managed by the '{_otherUser.Username}', '{otherUser2.Username}', and '{otherUser3.Username}' accounts.", viewModel.RequiredSignerMessage);
            VerifySigners(new[] { _otherUser }, viewModel.AllSigners, expectAnySigner: false);
            Assert.True(viewModel.ShowRequiredSigner);
            Assert.False(viewModel.ShowTextBox);
            Assert.False(viewModel.CanEditRequiredSigner);

            _securityPolicyService.VerifyAll();
        }

        private static void VerifySigners(
            IEnumerable<User> expectedSigners,
            IEnumerable<SignerViewModel> actualSigners,
            bool expectAnySigner)
        {
            var expectedSignersCount = expectedSigners.Count();

            if (expectAnySigner)
            {
                Assert.Equal(expectedSignersCount + 1, actualSigners.Count());

                var firstSigner = actualSigners.First();

                Assert.Equal(string.Empty, firstSigner.Username);
                Assert.Equal("Any", firstSigner.DisplayText);
            }
            else
            {
                Assert.Equal(expectedSignersCount, actualSigners.Count());
            }

            for (var i = 0; i < expectedSignersCount; ++i)
            {
                var expectedSigner = expectedSigners.ElementAt(i);
                var actualSigner = actualSigners.ElementAt(i + (expectAnySigner ? 1 : 0));

                Assert.Equal(expectedSigner.Username, actualSigner.Username);

                var activeCertificatesCount = expectedSigner.UserCertificates.Count();
                var expectedDisplayText = $"{expectedSigner.Username} ({activeCertificatesCount} certificate{(activeCertificatesCount == 1 ? "" : "s")})";

                Assert.Equal(expectedDisplayText, actualSigner.DisplayText);
            }
        }
    }
}