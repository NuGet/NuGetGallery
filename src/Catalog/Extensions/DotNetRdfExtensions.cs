// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
	/// <summary>
	/// Provides replacement extension methods for CopyNode and CopyTriple,
	/// which were removed in dotNetRDF 3.x.
	/// </summary>
	public static class DotNetRdfExtensions
	{
		public static INode CopyNode (this INode node, IGraph target)
		{
			if (node == null)
			{
				throw new ArgumentNullException (nameof (node));
			}

			if (target == null)
			{
				throw new ArgumentNullException (nameof (target));
			}

			switch (node)
			{
				case IUriNode uriNode:
					return target.CreateUriNode (uriNode.Uri);
				case ILiteralNode literalNode:
					if (!string.IsNullOrEmpty (literalNode.Language))
					{
						return target.CreateLiteralNode (literalNode.Value, literalNode.Language);
					}
					if (literalNode.DataType != null)
					{
						return target.CreateLiteralNode (literalNode.Value, literalNode.DataType);
					}
					return target.CreateLiteralNode (literalNode.Value);
				case IBlankNode blankNode:
					return target.CreateBlankNode (blankNode.InternalID);
				default:
					throw new ArgumentException ($"Unsupported node type: {node.GetType ()}", nameof (node));
			}
		}

		public static INode CopyNode (this INode node, IGraph target, bool keepOriginalGraphUri)
		{
			return CopyNode (node, target);
		}

		public static Triple CopyTriple (this Triple triple, IGraph target)
		{
			if (triple == null)
			{
				throw new ArgumentNullException (nameof (triple));
			}

			if (target == null)
			{
				throw new ArgumentNullException (nameof (target));
			}

			return new Triple (
				CopyNode (triple.Subject, target),
				CopyNode (triple.Predicate, target),
				CopyNode (triple.Object, target));
		}
	}
}
