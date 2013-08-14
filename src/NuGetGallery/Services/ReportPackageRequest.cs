using System;
using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Web.Mvc;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class ReportPackageRequest
    {
        public MailAddress FromAddress { get; set; }
        public User RequestingUser { get; set; }
        public Package Package { get; set; }
        public string Reason { get; set; }
        public string Message { get; set; }
        public bool AlreadyContactedOwners { get; set; }
        public UrlHelper Url { get; set; }

        internal string FillIn(string subject, IAppConfiguration config)
        {
            // note, format blocks {xxx} are matched by ordinal-case-sensitive comparison
            var ret = new StringBuilder(subject);
            Action<string, string> substitute = (target, value) => ret.Replace(target, Escape(value));

            substitute("{GalleryOwnerName}", config.GalleryOwner.DisplayName);
            substitute("{Id}", Package.PackageRegistration.Id);
            substitute("{Version}", Package.Version);
            substitute("{Reason}", Reason);
            if (RequestingUser != null)
            {
                substitute("{Username}", RequestingUser.Username);
                substitute("{UserUrl}", Url.User(RequestingUser, scheme: "http"));
                if (RequestingUser.EmailAddress != null)
                {
                    substitute("{UserAddress}", RequestingUser.EmailAddress);
                }
            }
            substitute("{Name}", FromAddress.DisplayName);
            substitute("{Address}", FromAddress.Address);
            substitute("{AlreadyContactedOwners}", AlreadyContactedOwners ? "Yes" : "No");
            substitute("{PackageUrl}", Url.Package(Package.PackageRegistration.Id, null, scheme: "http"));
            substitute("{VersionUrl}", Url.Package(Package.PackageRegistration.Id, Package.Version, scheme: "http"));
            substitute("{Reason}", Reason);
            substitute("{Message}", Message);

            var ownersText = new StringBuilder("");
            foreach (var owner in Package.PackageRegistration.Owners)
            {
                ownersText.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0} - {1} - ({2})",
                    owner.Username,
                    Url.User(owner, scheme: "http"),
                    owner.EmailAddress);
                ownersText.AppendLine();
            }

            substitute("{OwnerList}", ownersText.ToString());

            ret.Replace(@"\{\", "{");
            return ret.ToString();
        }

        private static string Escape(string s)
        {
            return s.Replace("{", @"\{\");
        }
    }
}