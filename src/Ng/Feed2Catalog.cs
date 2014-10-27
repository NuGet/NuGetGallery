using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Ng
{
    public static class Feed2Catalog
    {
        static Uri MakeCreatedUri(string source, DateTime since, int top = 100)
        {
            string address = string.Format("{0}/Packages?$filter=Created gt DateTime'{1}'&$top={2}&$orderby=Created&$select=Created",
                source.Trim('/'),
                since.ToString("o"),
                top);

            return new Uri(address);
        }

        static Uri MakeLastEditedUri(string source, DateTime since, int top = 100)
        {
            string address = string.Format("{0}/Packages?$filter=LastEdited gt DateTime'{1}'&$top={2}&$orderby=LastEdited&$select=LastEdited",
                source.Trim('/'),
                since.ToString("o"),
                top);

            return new Uri(address);
        }

        static async Task<SortedList<DateTime, IList<Uri>>> GetCreatedPackages(HttpClient client, string source, DateTime since, int top)
        {
            SortedList<DateTime, IList<Uri>> result = new SortedList<DateTime, IList<Uri>>();

            Uri uri = MakeCreatedUri(source, since, top);

            Trace.TraceInformation("FETCH {0}", uri);

            XElement feed = XElement.Load(await client.GetStreamAsync(uri));

            XNamespace atom = "http://www.w3.org/2005/Atom";
            XNamespace dataservices = "http://schemas.microsoft.com/ado/2007/08/dataservices";
            XNamespace metadata = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";

            foreach (XElement entry in feed.Elements(atom + "entry"))
            {
                Uri content = new Uri(entry.Element(atom + "content").Attribute("src").Value);
                DateTime date = DateTime.Parse(entry.Element(metadata + "properties").Element(dataservices + "Created").Value);

                IList<Uri> contentUri;
                if (!result.TryGetValue(date, out contentUri))
                {
                    contentUri = new List<Uri>();
                    result.Add(date, contentUri);
                }

                contentUri.Add(content);
            }

            return result;
        }

        static async Task<DateTime> DownloadMetadata2Catalog(HttpClient client, SortedList<DateTime, IList<Uri>> packages)
        {
            DateTime since = DateTime.Parse("2011-05-06T23:57:42.1030000");

            return since;
        }

        static async Task Loop()
        {
            DateTime since = DateTime.Parse("2011-05-06T23:57:42.1030000");

            int top = 100;
            int timeout = 300;

            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(timeout);

                SortedList<DateTime, IList<Uri>> packages;
                do
                {
                    packages = await GetCreatedPackages(client, "https://www.nuget.org/api/v2", since, top);

                    since = await DownloadMetadata2Catalog(client, packages);
                }
                while (packages.Count > 0);
            }
        }

        public static void Run(string[] args)
        {
            IDictionary<string, string> arguments = Utils.GetArguments(args, 1);

            foreach (var arg in arguments)
            {
                Console.WriteLine("{0} = {1}", arg.Key, arg.Value);
            }
        }
    }
}
