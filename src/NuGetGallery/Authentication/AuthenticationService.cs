using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.Authentication
{
    public class AuthenticationService
    {
        public IEntitiesContext Entities { get; private set; }

        public AuthenticationService(IEntitiesContext entities)
        {
            Entities = entities;
        }

        public virtual AuthenticateUserResult AuthenticateUser(string userNameOrEmail, string password)
        {

        }
    }
}