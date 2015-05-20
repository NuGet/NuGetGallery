// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;
using NuGet.Versioning;

namespace NuGet.Indexing
{
    public class SemanticVersionFilter : TokenFilter
    {
        ITermAttribute _termAttribute;

        public SemanticVersionFilter(TokenStream stream)
            : base(stream)
        {
            _termAttribute = AddAttribute<ITermAttribute>();
        }

        public override bool IncrementToken()
        {
            if (!input.IncrementToken())
            {
                return false;
            }

            string version = _termAttribute.Term;

            NuGetVersion nuGetVersion;
            if (NuGetVersion.TryParse(version, out nuGetVersion))
            {
                version = nuGetVersion.ToNormalizedString();
            }

            _termAttribute.SetTermBuffer(version);

            return true;
        }
    }
}
