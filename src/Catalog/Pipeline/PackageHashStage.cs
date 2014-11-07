using NuGet.Services.Metadata.Catalog;
using System;
using System.IO;
using System.Security.Cryptography;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Pipeline
{
    public class PackageHashStage : PackagePipelineStage
    {
        public override bool Execute(PipelinePackage package, PackagePipelineContext context)
        {
            string hash = GetHash(package.Stream);
            long size = GetSize(package.Stream);

            IGraph graph = new Graph();
            INode subject = graph.CreateUriNode(context.Uri);
            graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.PackageHashAlgorithm), graph.CreateLiteralNode("SHA512"));
            graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.PackageHash), graph.CreateLiteralNode(hash));
            graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.PackageSize), graph.CreateLiteralNode(size.ToString(), Schema.DataTypes.Integer));

            context.StageResults.Add(new GraphPackageMetadata(graph));

            return true;
        }

        static string GetHash(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            using (HashAlgorithm hashAlgorithm = HashAlgorithm.Create("SHA512"))
            {
                return Convert.ToBase64String(hashAlgorithm.ComputeHash(stream));
            }
        }

        static long GetSize(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            return stream.Length;
        }
    }
}
