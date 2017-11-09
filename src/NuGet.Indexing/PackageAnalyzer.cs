// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using LuceneConstants = NuGet.Indexing.MetadataConstants.LuceneMetadata;

namespace NuGet.Indexing
{
    public class PackageAnalyzer : PerFieldAnalyzerWrapper
    {
        static readonly IDictionary<string, Analyzer> _fieldAnalyzers;

        static PackageAnalyzer()
        {
            _fieldAnalyzers = new Dictionary<string, Analyzer>(StringComparer.OrdinalIgnoreCase)
            {
                { LuceneConstants.IdPropertyName, new IdentifierKeywordAnalyzer() },
                { LuceneConstants.IdAutocompletePropertyName, new IdentifierAutocompleteAnalyzer() },
                { LuceneConstants.TokenizedIdPropertyName, new IdentifierAnalyzer() },
                { LuceneConstants.ShingledIdPropertyName, new ShingledIdentifierAnalyzer() },
                { LuceneConstants.NormalizedVersionPropertyName, new VersionAnalyzer(caseSensitive: true) },
                { LuceneConstants.CaseInsensitiveNormalizedVersionPropertyName, new VersionAnalyzer(caseSensitive: false) },
                { LuceneConstants.TitlePropertyName, new DescriptionAnalyzer() },
                { LuceneConstants.DescriptionPropertyName, new DescriptionAnalyzer() },
                { LuceneConstants.SummaryPropertyName, new DescriptionAnalyzer() },
                { LuceneConstants.AuthorsPropertyName, new DescriptionAnalyzer() },
                { LuceneConstants.TagsPropertyName, new TagsAnalyzer() }
            };
        }

        public PackageAnalyzer()
            : base(new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30), _fieldAnalyzers)
        {
        }
    }
}
