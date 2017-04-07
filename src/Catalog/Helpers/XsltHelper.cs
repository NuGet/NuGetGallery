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
        public XPathNavigator Split(string original)
        {
            char[] trimChar = { ',', ' ', '\t', '|', ';' };

            IEnumerable<string> fields = original
                .Split(trimChar)
                .Select((w) => w.Trim(trimChar))
                .Where((w) => w.Length > 0);

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
            return NuGetVersionUtility.NormalizeVersionRange(original);
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
