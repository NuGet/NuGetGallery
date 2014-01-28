using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NuGet.Services.Operations.Model;

namespace NuGet.Services.Operations.Serialization
{
    public static class XmlSubscriptionParser
    {
        public static IList<Subscription> LoadSubscriptions(string path)
        {
            XDocument doc = XDocument.Load(path);
            return LoadSubscriptions(doc);
        }

        private static IList<Subscription> LoadSubscriptions(XDocument doc)
        {
            // Iterate over subscriptions and load them
            return doc.Root.Elements("subscription").Select(LoadSubscription).ToList();
        }

        private static Subscription LoadSubscription(XElement element)
        {
            return new Subscription()
            {
                Id = element.AttributeValue("id"),
                Name = element.AttributeValue("name")
            };
        }
    }
}
