using NuGet.Packaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    /// <summary>
    /// Data from nuget.packed.json
    /// </summary>
    public class PackedData : GraphAddon
    {
        private readonly IEnumerable<string> _supportedFrameworks;
        private readonly IEnumerable<ArtifactGroup> _assetGroups;

        public PackedData(IEnumerable<string> supportedFrameworks, IEnumerable<ArtifactGroup> assetGroups)
        {
            _supportedFrameworks = supportedFrameworks;
            _assetGroups = assetGroups;
        }

        public IEnumerable<string> SupportedFrameworks
        {
            get
            {
                return _supportedFrameworks;
            }
        }

        public IEnumerable<ArtifactGroup> AssetGroups
        {
            get
            {
                return _assetGroups;
            }
        }

        public static readonly Uri PackageAssetGroupPropertyType = new Uri(NuGet.Services.Metadata.Catalog.Schema.Prefixes.NuGet + "PackageAssetGroupProperty");
        public static readonly Uri PackageAssetGroupType = new Uri(NuGet.Services.Metadata.Catalog.Schema.Prefixes.NuGet + "PackageAssetGroup");
        public static readonly Uri AssetGroupPredicate = new Uri(NuGet.Services.Metadata.Catalog.Schema.Prefixes.NuGet + "assetGroup");
        public static readonly Uri AssetGroupPropertyPredicate = new Uri(NuGet.Services.Metadata.Catalog.Schema.Prefixes.NuGet + "assetGroupProperty");
        public static readonly Uri AssetKeyPredicate = new Uri(NuGet.Services.Metadata.Catalog.Schema.Prefixes.NuGet + "assetKey");
        public static readonly Uri AssetValuePredicate = new Uri(NuGet.Services.Metadata.Catalog.Schema.Prefixes.NuGet + "assetValue");
        public static readonly Uri PackageAssetType = new Uri(NuGet.Services.Metadata.Catalog.Schema.Prefixes.NuGet + "PackageAsset");
        public static readonly Uri AssetPredicate = new Uri(NuGet.Services.Metadata.Catalog.Schema.Prefixes.NuGet + "asset");
        public static readonly Uri AssetTypePredicate = new Uri(NuGet.Services.Metadata.Catalog.Schema.Prefixes.NuGet + "assetType");
        public static readonly Uri AssetPathPredicate = new Uri(NuGet.Services.Metadata.Catalog.Schema.Prefixes.NuGet + "assetPath");

        /// <summary>
        /// Add data from nuget.packed.json
        /// </summary>
        public override void ApplyToGraph(IGraph graph, IUriNode mainNode)
        {
            Uri mainUri = mainNode.Uri;
            IUriNode typeNode = graph.CreateUriNode(Schema.Predicates.Type);

            // supported frameworks
            if (SupportedFrameworks != null)
            {
                foreach (string framework in SupportedFrameworks)
                {
                    graph.Assert(new Triple(mainNode, graph.CreateUriNode(Schema.Predicates.SupportedFramework), graph.CreateLiteralNode(framework)));
                }
            }

            // assets
            if (AssetGroups != null)
            {
                int groupId = 0;
                foreach (var group in AssetGroups)
                {
                    // group type and id
                    var groupNode = GetSubNode(graph, mainUri, "assetGroup", "" + groupId);
                    graph.Assert(groupNode, typeNode, graph.CreateUriNode(PackageAssetGroupType));
                    graph.Assert(mainNode, graph.CreateUriNode(AssetGroupPredicate), groupNode);
                    groupId++;

                    int propId = 0;

                    // group properties
                    foreach (var prop in group.Properties)
                    {
                        var propNode = GetSubNode(graph, groupNode, "property", "" + propId);
                        propId++;
                        graph.Assert(propNode, typeNode, graph.CreateUriNode(PackageAssetGroupPropertyType));
                        graph.Assert(groupNode, graph.CreateUriNode(AssetGroupPropertyPredicate), propNode);
                        graph.Assert(propNode, graph.CreateUriNode(AssetKeyPredicate), graph.CreateLiteralNode(prop.Key));
                        graph.Assert(propNode, graph.CreateUriNode(AssetValuePredicate), graph.CreateLiteralNode(prop.Value));
                    }

                    int assetId = 0;

                    // group items
                    foreach (var item in group.Items)
                    {
                        var itemNode = GetSubNode(graph, groupNode, "asset", "" + assetId);
                        assetId++;
                        graph.Assert(itemNode, typeNode, graph.CreateUriNode(PackageAssetType));
                        graph.Assert(groupNode, graph.CreateUriNode(AssetPredicate), itemNode);

                        graph.Assert(itemNode, graph.CreateUriNode(AssetTypePredicate), graph.CreateLiteralNode(item.ArtifactType));
                        graph.Assert(itemNode, graph.CreateUriNode(AssetPathPredicate), graph.CreateLiteralNode(item.Path));
                    }
                }
            }
        }
    }
}
