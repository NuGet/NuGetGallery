using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;

namespace NuGetGallery.Configuration
{
    public class SmtpUri
    {
        private static readonly Regex UserInfoParser = new Regex("^(?<username>[^:]*):(?<password>.*)$");

        public string UserName { get; private set; }
        public string Password { get; private set; }
        public string Host { get; private set; }
        public int Port { get; private set; }
        public bool Secure { get; private set; }

        public SmtpUri(Uri uri)
        {
            Secure = uri.Scheme.Equals("smtps", StringComparison.OrdinalIgnoreCase);
            if (!Secure && !uri.Scheme.Equals("smtp", StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException("Invalid SMTP URL: " + uri.ToString());
            }

            var m = UserInfoParser.Match(uri.UserInfo);
            if (m.Success)
            {
                UserName = WebUtility.UrlDecode(m.Groups["username"].Value);
                Password = WebUtility.UrlDecode(m.Groups["password"].Value);
            }
            else
            {
                UserName = WebUtility.UrlDecode(uri.UserInfo);
            }
            Host = uri.Host;
            Port = uri.IsDefaultPort ? 25 : uri.Port;
        }
    }
}