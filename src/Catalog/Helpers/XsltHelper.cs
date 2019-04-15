// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.XPath;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog
{
    public class XsltHelper
    {
        /// <summary>
        /// Default to an empty string if the dependency version range is invalid or missing. This is meant to be a
        /// predictable signal to the client that they need to handle this invalid version case. The official NuGet
        /// client treats this as a dependency of any version.
        /// </summary>
        private static readonly string DefaultVersionRange = string.Empty;

        public XPathNavigator Split(string original)
        {
            var fields = Utils.SplitTags(original);

            XmlDocument xmlDoc = Utils.SafeCreateXmlDocument();
            XmlElement root = xmlDoc.CreateElement("list");
            xmlDoc.AppendChild(root);

            foreach (string s in fields)
            {
                XmlElement element = xmlDoc.CreateElement("item");
                element.InnerText = s;
                root.AppendChild(element);
            }

            return xmlDoc.CreateNavigator();
        }

        public string LowerCase(string original)
        {
            return original.ToLowerInvariant();
        }

        public string NormalizeVersion(string original)
        {
            return NuGetVersionUtility.NormalizeVersion(original);
        }

        public string GetFullVersionString(string original)
        {
            return NuGetVersionUtility.GetFullVersionString(original);
        }

        public string NormalizeVersionRange(string original)
        {
            return NuGetVersionUtility.NormalizeVersionRange(original, DefaultVersionRange);
        }
        
        public string IsPrerelease(string original)
        {
            NuGetVersion nugetVersion;
            if (NuGetVersion.TryParse(original, out nugetVersion))
            {
                return nugetVersion.IsPrerelease ? "true" : "false";
            }
            return "true";
        }
    }
}
