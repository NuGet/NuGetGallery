// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using System;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public class PackageAnalyzer : PerFieldAnalyzerWrapper
    {
        static readonly IDictionary<string, Analyzer> _fieldAnalyzers;

        static PackageAnalyzer()
        {
            _fieldAnalyzers = new Dictionary<string, Analyzer>(StringComparer.OrdinalIgnoreCase)
            {
                { "Id", new IdentifierKeywordAnalyzer() },
                { "IdAutocomplete", new IdentifierAutocompleteAnalyzer() },
                { "TokenizedId", new IdentifierAnalyzer() },
                { "ShingledId", new ShingledIdentifierAnalyzer() },
                { "Version", new VersionAnalyzer() },
                { "Title", new DescriptionAnalyzer() },
                { "Description", new DescriptionAnalyzer() },
                { "Summary", new DescriptionAnalyzer() },
                { "Authors", new DescriptionAnalyzer() },
                { "Owners", new OwnerAnalyzer() },
                { "Tags", new TagsAnalyzer() },
                { "__default", new KeywordAnalyzer() } 
            };
        }

        public PackageAnalyzer()
            : base(new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30), _fieldAnalyzers)
        {
        }
    }
}
