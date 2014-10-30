using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Canton
{
    public class GalleryGraphAddon : GraphAddon
    {
        private JObject _galleryPage;

        public GalleryGraphAddon(JObject galleryPage)
            : base()
        {
            _galleryPage = galleryPage;
        }

        public override void ApplyToGraph(IGraph graph, IUriNode parent)
        {
            Replace(graph, parent, Schema.Predicates.ProjectUrl, "projectUrl");
            Replace(graph, parent, Schema.Predicates.Title, "title");
            Replace(graph, parent, Schema.Predicates.IconUrl, "iconUrl");

            //Replace(graph, parent, Schema.Predicates.Summary, "summary");
            //Replace(graph, parent, Schema.Predicates.Author, "authors");
            //Replace(graph, parent, Schema.Predicates.Tag, "tag");
            //Replace(graph, parent, Schema.Predicates.ReleaseNotes, "releaseNotes");
        }

        private void Replace(IGraph graph, IUriNode parent, Uri predicate, string jsonField)
        {
            JToken token = null;
            if (_galleryPage.TryGetValue(jsonField, out token))
            {
                JArray array = token as JArray;
                JValue val = token as JValue;

                if (array != null || val != null)
                {
                    var pred = graph.CreateUriNode(predicate);

                    var old = graph.GetTriplesWithSubjectPredicate(parent, pred).ToArray();

                    // remove the old values
                    foreach (var triple in old)
                    {
                        graph.Retract(triple);
                    }

                    if (array != null)
                    {
                        foreach (var child in array)
                        {
                            graph.Assert(parent, pred, graph.CreateLiteralNode(child.ToString()));
                        }
                    }
                    else
                    {
                        graph.Assert(parent, pred, graph.CreateLiteralNode(val.ToString()));
                    }
                }
            }
        }

    }
}
