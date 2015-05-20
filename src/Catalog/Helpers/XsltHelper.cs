// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Versioning;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.XPath;

namespace NuGet.Services.Metadata.Catalog
{
    public class XsltHelper
    {
        public XPathNavigator Split(string original)
        {
            char[] trimChar = { ',', ' ', '\t', '|', ';' };

            IEnumerable<string> fields = original
                .Split(trimChar)
                .Select((w) => w.Trim(trimChar))
                .Where((w) => w.Length > 0);

            XmlDocument xmlDoc = new XmlDocument();
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
            NuGetVersion nugetVersion;
            if (NuGetVersion.TryParse(original, out nugetVersion))
            {
                return nugetVersion.ToNormalizedString();
            }
            return original;
        }

        public string NormalizeVersionRange(string original)
        {
            VersionRange versionRange;
            if (VersionRange.TryParse(original, out versionRange))
            {
                return versionRange.ToString();
            }
            return original;
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
