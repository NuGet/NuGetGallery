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
    }
}
