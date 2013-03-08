using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    /// <summary>
    /// Token that holds the original data recieved from the OAuth Provider.
    /// </summary>
    public class OAuthLinkToken
    {
        internal static readonly string CryptoPurpose = "OAuthLinkToken";

        public string EmailAddress { get; set; }
        public string UserName { get; set; }
        public string Provider { get; set; }
        public string Id { get; set; }

        /// <summary>
        /// Calculate an _unencrypted_ token that can be used to round-trip the data to the client
        /// </summary>
        /// <remarks>
        /// This should be encrypted using ICryptographyService before sending to the client.
        /// </remarks>
        public static string CalculateToken(string email, string userName, string id, string provider)
        {
            return String.Join("|", email, userName, id, provider);
        }

        /// <summary>
        /// Restores the token from the decrypted serialized form
        /// </summary>
        public static OAuthLinkToken FromToken(string decryptedToken)
        {
            string[] parsed = decryptedToken.Split('|');
            if (parsed.Length != 4)
            {
                throw new ArgumentException("Invalid token", "token");
            }

            return new OAuthLinkToken()
            {
                EmailAddress = parsed[0],
                UserName = parsed[1],
                Id = parsed[2],
                Provider = parsed[3]
            };
        }
    }
}