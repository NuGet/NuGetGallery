using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NuGet.Services.Operations.Model;

namespace NuGet.Services.Operations.Serialization
{
    public static class XmlEnvironmentParser
    {
        public static IList<NuOpsEnvironment> LoadEnvironments(string path, bool strict = false)
        {
            XDocument doc = XDocument.Load(path);
            return LoadEnvironments(doc, strict);
        }

        private static IList<NuOpsEnvironment> LoadEnvironments(XDocument doc, bool strict = false)
        {
            // Verify the version
            var version = doc.Root.AttributeValue("version");
            if (!String.Equals(Constants.EnvironmentDefinitionVersion, version, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Invalid Environment Definition file. Expected version " + Constants.EnvironmentDefinitionVersion + " but the file version is: " + version);
            }

            // Iterate over environments and load them
            return doc.Root.Elements("environment").Select(x =>
            {
                try
                {
                    return LoadEnvironment(x);
                }
                catch (Exception)
                {
                    if (strict)
                    {
                        throw;
                    }
                    else
                    {
                        return null;
                    }
                }
            }).Where(e => e != null).ToList();
        }

        private static NuOpsEnvironment LoadEnvironment(XElement root)
        {
            NuOpsEnvironment env = new NuOpsEnvironment();
            env.Name = root.AttributeValue("name");
            
            env.Version = root.AttributeValueAs<Version>("version", Version.Parse, new Version(2, 0));
            env.Subscription = root.AttributeValueAs<Subscription>("subscription", s => new Subscription() { Name = s });
            
            // Load datacenters
            foreach (var dcXml in root.Elements("datacenter"))
            {
                var dc = LoadDatacenter(dcXml);
                env.Datacenters.Add(dc.Id, dc);
            }
            return env;
        }

        private static Datacenter LoadDatacenter(XElement root)
        {
            Datacenter dc = new Datacenter();
            dc.Id = root.AttributeValueAs<int>("id", Int32.Parse);

            dc.Region = root.AttributeValue("region");
            dc.AffinityGroup = root.AttributeValue("affinityGroup");

            LoadList(dc.PackageSources, x => LoadDatacenterComponent<PackageSource>(x), root.Element("packageSources"));
            LoadList(dc.SecretStores, x => LoadDatacenterComponent<SecretStore>(x), root.Element("secretStores"));
            LoadList(dc.Resources, x => LoadDatacenterComponent<Resource>(x), root.Element("resources"));
            LoadList(dc.Services, x => LoadDatacenterComponent<Service>(x), root.Element("services"));

            return dc;
        }

        private static T LoadDatacenterComponent<T>(XElement element)
            where T : DatacenterComponentBase, new()
        {
            // Find the parser
            string parserName = typeof(IParser<T>).Namespace + "." + element.Name.LocalName + typeof(T).Name + "Parser";
            Type typ = typeof(XmlEnvironmentParser).Assembly.GetType(parserName, throwOnError: false, ignoreCase: true);
            if (typ == null)
            {
                // Load the default data
                return new T()
                {
                    Name = element.AttributeValue("name"),
                    Type = element.Name.LocalName
                };
            }
            IParser<T> parser = (IParser<T>)Activator.CreateInstance(typ);
            return parser.Parse(element);
        }

        private static void LoadList<T>(IList<T> list, Func<XElement, T> loader, XElement element)
            where T : DatacenterComponentBase
        {
            if (element != null)
            {
                foreach (var item in element.Elements())
                {
                    list.Add(loader(item));
                }
            }
        }
    }
}
