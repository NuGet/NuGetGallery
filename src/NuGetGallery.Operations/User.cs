using System.Collections.Generic;
namespace NuGetGallery.Operations
{
    public class User
    {
        public int Key { get; set; }
        public string Username { get; set; }
        public string EmailAddress { get; set; }
        public string UnconfirmedEmailAddress { get; set; }

        public IEnumerable<string> PackageRegistrationIds { get; set; }
    }
}
