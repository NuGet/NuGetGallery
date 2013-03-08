using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Security;

namespace NuGetGallery
{
    public class LinkOrCreateViewModel
    {
        internal static readonly string OAuthLinkingMachineKeyPurpose = "OAuthLinkToken";

        public string EmailAddress { get; set; }
        public string UserName { get; set; }
        public string Provider { get; set; }
        public string Id { get; set; }

        public string Token
        {
            get
            {
                return CalculateToken(EmailAddress, UserName, Id, Provider);
            }
        }

        public static string CalculateToken(string email, string userName, string id, string provider)
        {
            string formatted = String.Join("|", email, userName, id, provider);
            return Convert.ToBase64String(
                MachineKey.Protect(
                    Encoding.UTF8.GetBytes(formatted),
                    OAuthLinkingMachineKeyPurpose));
        }

        public static LinkOrCreateViewModel FromToken(string token)
        {
            string formatted = Encoding.UTF8.GetString(
                MachineKey.Unprotect(
                    Convert.FromBase64String(token),
                    OAuthLinkingMachineKeyPurpose));
            string[] parsed = formatted.Split('|');
            if (parsed.Length != 4)
            {
                throw new ArgumentException("Invalid token", "token");
            }

            return new LinkOrCreateViewModel()
            {
                EmailAddress = parsed[0],
                UserName = parsed[1],
                Id = parsed[2],
                Provider = parsed[3]
            };
        }
    }
}
