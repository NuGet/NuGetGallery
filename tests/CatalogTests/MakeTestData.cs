using System;
using System.Xml;
using System.Xml.Linq;

namespace CatalogTests
{
    class MakeTestData
    {
        public static XDocument MakeNuspec(string id, string version)
        {
            XNamespace nuget = XNamespace.Get("http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd");
   
            XElement package = 
                new XElement(nuget.GetName("package"),
                    new XElement(nuget.GetName("metadata"),
                        new XElement(nuget.GetName("id"), id),
                        new XElement(nuget.GetName("version"), version),
                        new XElement(nuget.GetName("authors"), "Test.Metadata.Service"),
                        new XElement(nuget.GetName("licenseUrl"), "Test.Metadata.Service"),
                        new XElement(nuget.GetName("description"), "Test package"),
                        new XElement(nuget.GetName("summary"), "Test package"),
                        new XElement(nuget.GetName("language"), "en-US")
                    )
                );

            XDocument doc = new XDocument();
            doc.Add(package);

            return doc;
        }

        public static void WriteNuspec(string path, string id, string version)
        {
            XDocument nuspec = MakeNuspec(id, version);

            path = path.TrimEnd('\\') + "\\";

            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true
            };

            string filename = path + id + "." + version + ".xml";

            using (XmlWriter writer = XmlWriter.Create(filename, settings))
            {
                nuspec.WriteTo(writer);
            }
        }

        public static void Test0()
        {
            Console.WriteLine("MakeTestData.Test0");

            string path = @"c:\data\Demo\Third";
            string id = "Test.Metadata.Service";

            for (int i=1; i<=500; i++)
            {
                string version = string.Format("{0}.0.0", i);
                WriteNuspec(path, id, version);
            }
        }
    }
}
