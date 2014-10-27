using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Xml;
using System.Xml.Xsl;

namespace NuGet.Services.Metadata.Catalog
{
    public class CatalogContext
    {
        ConcurrentDictionary<string, XslCompiledTransform> _compiledTransforms;
        ConcurrentDictionary<string, JObject> _jsonLdContext;
        Func<string, Stream> _getStream;

        public CatalogContext(Func<string, Stream> getStream)
        {
            _compiledTransforms = new ConcurrentDictionary<string, XslCompiledTransform>();
            _jsonLdContext = new ConcurrentDictionary<string, JObject>();
            _getStream = getStream;
        }

        public CatalogContext()
            : this(Utils.GetResourceStream)
        {
        }

        public XslCompiledTransform GetXslt(string name)
        {
            return _compiledTransforms.GetOrAdd(name, (key) =>
            {
                XslCompiledTransform xslt = new XslCompiledTransform();
                xslt.Load(XmlReader.Create(new StreamReader(GetStream(name))));
                return xslt;
            });
        }

        public JObject GetJsonLdContext(string name, Uri type)
        {
            return _jsonLdContext.GetOrAdd(name + "#" + type.ToString(), (key) =>
            {
                using (JsonReader jsonReader = new JsonTextReader(new StreamReader(GetStream(name))))
                {
                    JObject obj = JObject.Load(jsonReader);
                    obj["@type"] = type.ToString();
                    return obj;
                }
            });
        }

        Stream GetStream(string name)
        {
            Stream stream = _getStream(name);
            if (stream == null)
            {
                throw new Exception(string.Format("unable to load: {0}", name));
            }
            return stream;
        }
    }
}
