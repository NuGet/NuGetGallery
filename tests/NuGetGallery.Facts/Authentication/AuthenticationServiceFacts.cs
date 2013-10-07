using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Authentication
{
    public class AuthenticationServiceFacts
    {
        public void GivenNoUserWithName_ItReturnsNoSuchUserResult()
        {
            // Arrange
            var entities = SetupFakeEntities();
        }
    }
}
