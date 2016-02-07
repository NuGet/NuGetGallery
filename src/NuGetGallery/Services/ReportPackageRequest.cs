﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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
        public string Signature { get; set; }
        public string Message { get; set; }
        public bool AlreadyContactedOwners { get; set; }
        public UrlHelper Url { get; set; }
        public bool CopySender { get; set; }

        internal string FillIn(string subject, IAppConfiguration config, int supportRequestID = -1)
        {
            // note, format blocks {xxx} are matched by ordinal-case-sensitive comparison
            var builder = new StringBuilder(subject);

            Substitute(builder, "{GalleryOwnerName}", config.GalleryOwner.DisplayName);
            Substitute(builder, "{Id}", Package.PackageRegistration.Id);
            Substitute(builder, "{Version}", Package.Version);
            Substitute(builder, "{Reason}", Reason);
            if (RequestingUser != null)
            {
                Substitute(builder, "{User}", String.Format(
                    CultureInfo.CurrentCulture,
                    "{2}**User:** {0} ({1}){2}{3}",
                    RequestingUser.Username,
                    RequestingUser.EmailAddress,
                    Environment.NewLine,
                    Url.User(RequestingUser, scheme: "http")));
            }
            else
            {
                Substitute(builder, "{User}", "");
            }
            Substitute(builder, "{Name}", FromAddress.DisplayName);
            Substitute(builder, "{Address}", FromAddress.Address);
            Substitute(builder, "{AlreadyContactedOwners}", AlreadyContactedOwners ? "Yes" : "No");
            Substitute(builder, "{PackageUrl}", Url.Package(Package.PackageRegistration.Id, null, scheme: "http"));
            Substitute(builder, "{VersionUrl}", Url.Package(Package.PackageRegistration.Id, Package.Version, scheme: "http"));
            Substitute(builder, "{Reason}", Reason);
            Substitute(builder, "{Signature}", Signature);
            Substitute(builder, "{Message}", Message);

            if (supportRequestID != -1)
            {
                var srLink = String.Concat(VerifyAndFixTralingSlash(config.SiteRoot), "Admin/SupportRequest/Edit/" + supportRequestID);
                Substitute(builder, "{SupportRequestLink}", srLink);
            }

            builder.Replace(@"\{\", "{");
            return builder.ToString();
        }

        private static string VerifyAndFixTralingSlash(string url)
        {
            var returnVal = url;
            if (!String.IsNullOrEmpty(url) && url.Substring(url.Length - 1, 1) != "/")
            {
                returnVal = String.Concat(url, "/");
            }
            return returnVal;
        }

        private static void Substitute(StringBuilder src, string input, string replacement)
        {
            src.Replace(input, Escape(replacement));
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            return s.Replace("{", @"\{\");
        }
    }
}