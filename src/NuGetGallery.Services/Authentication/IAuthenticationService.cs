using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery.Services.Authentication
{
    public interface IAuthenticationService
    {
        Task RemoveCredential(User user, Credential cred, bool commitChanges);
    }
}
