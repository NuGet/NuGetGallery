// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NuGetGallery
{
    internal struct ObfuscateMetadata
    {
        /// <summary>
        /// A <see cref="System.Text.RegularExpressions.Regex" to be used for matching the value to be obfuscated./>
        /// </summary>
        public string ObfuscateTemplate
        {
            get;
        }

        /// <summary>
        /// The obfuscation value.
        /// </summary>
        public string ObfuscateValue
        {
            get;
        }


        public ObfuscateMetadata(string obfuscateTemplate, string obfuscateValue)
        {
            ObfuscateTemplate = obfuscateTemplate;
            ObfuscateValue = obfuscateValue;
        }
    }

    internal static class Obfuscator
    {
        /// <summary>
        /// Default user name that will replace the real user name.
        /// This value will be saved in AI instead of the real value.
        /// </summary>
        internal const string DefaultTelemetryUserName = "ObfuscatedUserName";

        internal static readonly HashSet<string> ObfuscatedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Packages/ConfirmPendingOwnershipRequest",
            "Packages/RejectPendingOwnershipRequest",
            "Packages/CancelPendingOwnershipRequest",
            "Users/Confirm",
            "Users/Delete",
            "Users/Profiles",
            "Users/ResetPassword"};

        internal static readonly Dictionary<string, ObfuscateMetadata> ObfuscatedUrlTemplates = new Dictionary<string, ObfuscateMetadata>
        {
            {@"/packages/(.+)/owners/(.+)/confirm/(.+)", new ObfuscateMetadata(@"/owners/(.+)/confirm", $"/owners/{DefaultTelemetryUserName}/confirm")},
            {@"/packages/(.+)/owners/(.+)/reject/(.+)", new ObfuscateMetadata(@"/owners/(.+)/reject", $"/owners/{DefaultTelemetryUserName}/reject") },
            {@"/packages/(.+)/owners/(.+)/cancel/(.+)", new ObfuscateMetadata(@"/owners/(.+)/cancel", $"/owners/{DefaultTelemetryUserName}/cancel") },
            {@"/account/confirm/(.+)/(.+)", new ObfuscateMetadata(@"/account/confirm/(.+)/", $"/account/confirm/{DefaultTelemetryUserName}/") },
            {@"/account/delete/(.+)", new ObfuscateMetadata(@"/account/delete/(.+)", $"/account/delete/{DefaultTelemetryUserName}") },
            {@"/profiles/(.+)", new ObfuscateMetadata(@"/profiles/(.+)", $"/profiles/{DefaultTelemetryUserName}") },
            {@"/account/setpassword/(.+)/(.+)", new ObfuscateMetadata(@"/setpassword/(.+)/", $"/setpassword/{DefaultTelemetryUserName}/") },
            {@"/account/forgotpassword/(.+)/(.+)", new ObfuscateMetadata(@"/forgotpassword/(.+)/", $"/forgotpassword/{DefaultTelemetryUserName}/") }
         };

        internal static string DefaultObfuscatedUrl(Uri url)
        {
            return url == null ? string.Empty : $"{url.Scheme}://{url.Host}/{DefaultTelemetryUserName}";
        }

        internal static Uri ObfuscateUrl(Uri url)
        {
            if (url == null)
            {
                return url;
            }
            string obfuscatedTemplateKey = null;
            if (NeedsObfuscation(url.AbsolutePath, out obfuscatedTemplateKey))
            {
                string obfuscatedPath = Regex.Replace(url.AbsolutePath,
                                            ObfuscatedUrlTemplates[obfuscatedTemplateKey].ObfuscateTemplate,
                                            ObfuscatedUrlTemplates[obfuscatedTemplateKey].ObfuscateValue);
                return new Uri($"{url.Scheme}://{url.Host}{obfuscatedPath}");
            }
            return url;
        }

        internal static string ObfuscateName(string name)
        {
            if(name == null)
            {
                return name;
            }
            string obfuscatedTemplateKey = null;
            if (NeedsObfuscation(name, out obfuscatedTemplateKey))
            {
                string obfuscatedPath = Regex.Replace(name,
                                            ObfuscatedUrlTemplates[obfuscatedTemplateKey].ObfuscateTemplate,
                                            ObfuscatedUrlTemplates[obfuscatedTemplateKey].ObfuscateValue);
                return obfuscatedPath;
            }
            return name;
        }

        internal static bool NeedsObfuscation(string valueToBeVerified, out string obfuscatedTemplateKey)
        {
            obfuscatedTemplateKey = string.Empty;
            try
            {
                var match = ObfuscatedUrlTemplates.Where(template => Regex.IsMatch(valueToBeVerified.ToLower(), template.Key)).FirstOrDefault();
                if (match.Key == null)
                {
                    return false;
                }
                obfuscatedTemplateKey = match.Key;
            }
            catch(Exception e)
            {
                var s = e.Message;
            }
            return true;
        }
    }
}