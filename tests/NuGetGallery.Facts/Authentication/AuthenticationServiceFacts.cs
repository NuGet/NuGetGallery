using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Moq;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Authentication
{
    public class AuthenticationServiceFacts
    {
        public class TheAuthenticateMethod : TestContainer
        {
            [Fact]
            public void GivenNoUserWithName_ItReturnsNull()
            {
                // Arrange
                var service = Get<AuthenticationService>();

                // Act
                var result = service.Authenticate("notARealUser", "password");

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public void GivenUserNameDoesNotMatchPassword_ItReturnsNull()
            {
                // Arrange
                var service = Get<AuthenticationService>();

                // Act
                var result = service.Authenticate(Fakes.User.Username, "bogus password!!");

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public void GivenUserNameWithMatchingPasswordCredential_ItReturnsAuthenticatedUser()
            {
                // Arrange
                var service = Get<AuthenticationService>();

                // Act
                var result = service.Authenticate(Fakes.User.Username, Fakes.Password);

                // Assert
                var expectedCred = Fakes.User.Credentials.SingleOrDefault(
                    c => String.Equals(c.Type, CredentialTypes.Password.Pbkdf2, StringComparison.OrdinalIgnoreCase));
                Assert.NotNull(result);
                Assert.Same(Fakes.User, result.User);
                Assert.Same(expectedCred, result.CredentialUsed);
            }

            // We don't normally test exception conditions, but it's really important that
            // this overload is NOT used for Passwords since every call to generate a Password Credential
            // uses a new Salt and thus produces a value that cannot be looked up in the DB. Instead,
            // we must look up the user and then verify the salted password hash.
            [Fact]
            public void GivenPasswordCredential_ItThrowsArgumentException()
            {
                // Arrange
                var service = Get<AuthenticationService>();
                var cred = CredentialBuilder.CreatePbkdf2Password("bogus");

                // Act
                var ex = Assert.Throws<ArgumentException>(() =>
                    service.Authenticate(cred));

                // Assert
                Assert.Equal(Strings.PasswordCredentialsCannotBeUsedHere + Environment.NewLine + "Parameter name: credential", ex.Message);
                Assert.Equal("credential", ex.ParamName);
            }

            [Fact]
            public void GivenInvalidApiKeyCredential_ItReturnsNull()
            {
                // Arrange
                var service = Get<AuthenticationService>();

                // Act
                var result = service.Authenticate(CredentialBuilder.CreateV1ApiKey());

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public void GivenMatchingApiKeyCredential_ItReturnsTheUserAndMatchingCredential()
            {
                // Arrange
                var service = Get<AuthenticationService>();
                var cred = Fakes.User.Credentials.Single(
                    c => String.Equals(c.Type, CredentialTypes.ApiKeyV1, StringComparison.OrdinalIgnoreCase));
                
                // Act
                // Create a new credential to verify that it's a value-based lookup!
                var result = service.Authenticate(CredentialBuilder.CreateV1ApiKey(Guid.Parse(cred.Value)));

                // Assert
                Assert.NotNull(result);
                Assert.Same(Fakes.User, result.User);
                Assert.Same(cred, result.CredentialUsed);
            }

            [Fact]
            public void GivenMultipleMatchingCredentials_ItThrows()
            {
                // Arrange
                var service = Get<AuthenticationService>();
                var entities = Get<IEntitiesContext>();
                var cred = CredentialBuilder.CreateV1ApiKey();
                cred.Key = 42;
                var creds = entities.Set<Credential>();
                creds.Add(cred);
                creds.Add(CredentialBuilder.CreateV1ApiKey(Guid.Parse(cred.Value)));

                // Act
                var ex = Assert.Throws<InvalidOperationException>(() => service.Authenticate(CredentialBuilder.CreateV1ApiKey(Guid.Parse(cred.Value))));

                // Assert
                Assert.Equal(String.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MultipleMatchingCredentials,
                    cred.Type,
                    cred.Key), ex.Message);
            }
        }

        public class TheCreateSessionMethod : TestContainer
        {
            [Fact]
            public void GivenAUser_ItCreatesAnOwinAuthenticationTicketForTheUser()
            {
                // Arrange
                var service = Get<AuthenticationService>();
                ClaimsIdentity id = null;
                GetMock<IOwinContext>()
                    .Setup(c => c.Authentication.SignIn(It.IsAny<ClaimsIdentity[]>()))
                    .Callback<ClaimsIdentity[]>(ids => id = ids.SingleOrDefault());
                
                var passwordCred = Fakes.Admin.Credentials.SingleOrDefault(
                    c => String.Equals(c.Type, CredentialTypes.Password.Pbkdf2, StringComparison.OrdinalIgnoreCase));

                var user = new AuthenticatedUser(Fakes.Admin, passwordCred);

                // Act
                service.CreateSession(user);

                // Assert
                Assert.NotNull(id);
                var principal = new ClaimsPrincipal(id);
                Assert.Equal(Fakes.Admin.Username, id.Name);
                Assert.Equal(passwordCred.Type, id.AuthenticationType);
                Assert.True(principal.IsInRole(Constants.AdminRoleName));
            }
        }
    }
}