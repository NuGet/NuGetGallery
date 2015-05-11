// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace NuGet.Services.Metadata.Catalog
{
    class XmlHtmlWriter : XmlTextWriter
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
            fullEndElement.Add("div");
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