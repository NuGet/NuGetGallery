using System;
using System.Security.Claims;

namespace NuGet.Services.Metadata.Catalog.Ownership
{
    public class OwnershipOwner
    {
        public string NameIdentifier { get; set; }
        public string Name { get; set; }
        public string GivenName { get; set; }
        public string Surname { get; set; }
        public string Email { get; set; }
        public string Iss { get; set; }

        public Uri GetUri(Uri baseAddress)
        {
            string fragment = string.Format("#owner/{0}", NameIdentifier);
            return new Uri(baseAddress, fragment);
        }

        public static OwnershipOwner Create(ClaimsPrincipal claimsPrinciple)
        {
            return new OwnershipOwner
            {
                NameIdentifier = Get(claimsPrinciple, ClaimTypes.NameIdentifier, true),
                Name = Get(claimsPrinciple, ClaimTypes.Name),
                GivenName = Get(claimsPrinciple, ClaimTypes.GivenName),
                Surname = Get(claimsPrinciple, ClaimTypes.Surname),
                Email = Get(claimsPrinciple, ClaimTypes.Email),
                Iss = Get(claimsPrinciple, "iss")
            };
        }

        static string Get(ClaimsPrincipal claimsPrinciple, string type, bool isRequired = true)
        {
            Claim subject = ClaimsPrincipal.Current.FindFirst(type);
            if (subject == null)
            {
                if (isRequired)
                {
                    throw new Exception(string.Format("required Claim {0} not found", type));
                }
                else
                {
                    return null;
                }
            }
            return subject.Value;
        }
    }
}
