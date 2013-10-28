using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Web;
using NuGetGallery.Authentication.Providers;

namespace NuGetGallery
{
    public class LogOnViewModel
    {
        public SignInRequest SignIn { get; set; }
        public RegisterRequest Register { get; set; }
        public bool AssociatingExternalLogin { get; set; }
    }
}