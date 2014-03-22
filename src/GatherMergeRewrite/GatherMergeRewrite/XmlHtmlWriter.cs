using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace GatherMergeRewrite
{
    public class XmlHtmlWriter : XmlTextWriter
    {
        HashSet<string> fullEndElement = new HashSet<string>();
        string openingElement = "";

        public XmlHtmlWriter(Stream stream, Encoding en)
            : base(stream, en)
        {
            Init();
        }

        public XmlHtmlWriter(TextWriter writer)
            : base(writer)
        {
            Init();
        }

        void Init()
        {
            fullEndElement.Add("script");
        }

        public override void WriteEndElement()
        {
            if (fullEndElement.Contains(openingElement))
            {
                WriteFullEndElement();
            }
            else
            {
                base.WriteEndElement();
            }
        }

        public override void WriteStartElement(string prefix, string localName, string ns)
        {
            base.WriteStartElement(prefix, localName, ns);
            openingElement = localName;
        }
    }
}