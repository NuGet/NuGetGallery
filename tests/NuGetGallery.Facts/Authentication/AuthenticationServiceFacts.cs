using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Authentication
{
    public class AuthenticationServiceFacts
    {
        public class TheAuthenticateUserMethod : TestContainer
        {
            [Fact]
            public void GivenNoUserWithName_ItReturnsNoSuchUserResult()
            {
                // Arrange
                var service = Get<AuthenticationService>();

                // Act
                var result = service.AuthenticateUser("notARealUser", "password");

                // Assert
                Assert.Equal(AuthenticateUserResultStatus.NoSuchUser, result.Status);
            }
        }
    }
}
