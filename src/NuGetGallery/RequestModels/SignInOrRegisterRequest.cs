using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class SignInOrRegisterRequest
    {
        public SignInRequest SignInRequest { get; set; }
        public RegisterRequest RegisterRequest { get; set; }
    }
}