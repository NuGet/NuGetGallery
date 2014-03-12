using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Writing;

namespace TestNuspec
{
    class Program
    {
        static XslCompiledTransform CreateTransform(string path)
        {
            XslCompiledTransform transform = new XslCompiledTransform();
            transform.Load(XmlReader.Create(new StreamReader(path)));
            return transform;
        }

        static void Main(string[] args)
        {
            string path = @"..\..\..\..\..\src\MakeMetadata\MakeMetadata\nuspec2package.xslt";

            XslCompiledTransform transform = CreateTransform(path);

            string baseAddress = "http://tempuri.org/base/";

            XsltArgumentList arguments = new XsltArgumentList();
            arguments.AddParam("base", "", baseAddress);

            //foreach (KeyValuePair<string, string> arg in transformArgs)
            //{
            //    arguments.AddParam(arg.Key, "", arg.Value);
            //}

            XDocument nuspec = XDocument.Load(new StreamReader(@"c:\data\extended\ledzep.boxset.v1.nuspec"));

            XDocument rdfxml = new XDocument();
            using (XmlWriter writer = rdfxml.CreateWriter())
            {
                transform.Transform(nuspec.CreateReader(), arguments, writer);
            }

            Console.WriteLine(rdfxml);

            Console.WriteLine();

            RdfXmlParser rdfXmlParser = new RdfXmlParser();
            XmlDocument doc = new XmlDocument();
            doc.Load(rdfxml.CreateReader());
            IGraph graph = new Graph();
            rdfXmlParser.Load(graph, doc);

            string subject = graph.GetTriplesWithPredicateObject(
                    graph.CreateUriNode(new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#type")),
                    graph.CreateUriNode(new Uri("http://nuget.org/schema#Package")))
                .First()
                .Subject
                .ToString();

            CompressingTurtleWriter turtleWriter = new CompressingTurtleWriter();
            turtleWriter.DefaultNamespaces.AddNamespace("nuget", new Uri("http://nuget.org/schema#"));
            turtleWriter.DefaultNamespaces.AddNamespace("package", new Uri(subject + "#"));

            turtleWriter.PrettyPrintMode = true;
            turtleWriter.CompressionLevel = 10;
            turtleWriter.Save(graph, Console.Out);
        }
    }
}
