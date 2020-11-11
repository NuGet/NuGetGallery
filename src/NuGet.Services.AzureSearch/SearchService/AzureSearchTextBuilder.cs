// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Services.AzureSearch.SearchService
{
    public partial class SearchTextBuilder
    {
        private enum Operator
        {
            None, // Default Lucene behavior, essentially "OR"
            Required, // "+" operator
            Prohibit, // "-" operator
        }

        /// <summary>
        /// Used to build Azure Search Service queries.
        /// </summary>
        /// <remarks>
        /// This generates Azure Search queries that use the Lucene query syntax.
        /// See: https://docs.microsoft.com/en-us/azure/search/query-lucene-syntax
        ///
        /// Given the query "fieldA:value1 value2":
        ///
        ///   * "value1" is a field-scoped term
        ///   * "value2" is an unscoped term
        /// </remarks>
        private class AzureSearchTextBuilder
        {
            /// <summary>
            /// Azure Search Queries must have less than 1024 clauses.
            /// See: https://docs.microsoft.com/en-us/azure/search/query-lucene-syntax#bkmk_querysizelimits
            /// </summary>
            private const int MaxClauses = 1024;

            /// <summary>
            /// Terms in Azure Search Queries must be less than 32KB.
            /// See: https://docs.microsoft.com/en-us/azure/search/query-lucene-syntax#bkmk_querysizelimits
            /// </summary>
            private const int MaxTermSizeBytes = 32 * 1024;

            /// <summary>
            /// These characters have special meaning in Azure Search and must be escaped if in user input.
            /// See: https://docs.microsoft.com/en-us/azure/search/query-lucene-syntax#escaping-special-characters
            /// </summary>
            private static readonly HashSet<char> SpecialCharacters = new HashSet<char>
            {
                '+', '-', '&', '|', '!', '(', ')', '{', '}', '[', ']', '^', '"', '~', '*', '?', ':', '\\', '/',
            };

            private readonly StringBuilder _result;
            private int _clauses;

            public AzureSearchTextBuilder()
            {
                _result = new StringBuilder();
                _clauses = 0;
            }

            /// <summary>
            /// Append a phrase that matches all values for the provided field.
            /// </summary>
            /// <param name="fieldName">The name of the field to match on.</param>
            public void AppendMatchAll(string fieldName)
            {
                _result.Append(fieldName);
                _result.Append(":/.*/");
            }

            /// <summary>
            /// Append a phase requiring at least one of the provided term sets. There must be at least two items in
            /// <paramref name="alternatives"/> and each alternative must have at least one option. If the alternatives
            /// are: <code>[ ['ab'], ['a', 'b'] ]</code>, then matched documents must have 'ab' OR both 'a' and 'b'.
            /// A document with just 'a' and not 'b' (or vice versa) would not match.
            /// </summary>
            /// <param name="alternatives">The array of alternatives where each collection includes one or more options.</param>
            /// <param name="prefixSearchSingleOptions">Whether or not perform a prefix search on alternatives with a single option.</param>
            public void AppendRequiredAlternatives(ICollection<string>[] alternatives, bool prefixSearchSingleOptions)
            {
                if (alternatives.Any(x => !x.Any()))
                {
                    throw new ArgumentException(
                        "Each alternative must have at least one option.",
                        nameof(alternatives));
                }

                if (alternatives.Length < 2)
                {
                    throw new ArgumentException(
                        "There must be at least two alternatives provided.",
                        nameof(alternatives));
                }

                AppendSpaceIfNotEmpty();

                _result.Append("+(");
                
                for (int i = 0; i < alternatives.Length; i++)
                {
                    if (i > 0)
                    {
                        _result.Append(' ');
                    }

                    var alternative = alternatives[i];
                    if (alternative.Count < 2)
                    {
                        AppendEscapedString(alternative.Single(), quoteWhiteSpace: false);
                        if (prefixSearchSingleOptions)
                        {
                            _result.Append('*');
                        }
                    }
                    else
                    {
                        _result.Append('(');
                        var counter = 0;
                        foreach (var option in alternative)
                        {
                            if (counter > 0)
                            {
                                _result.Append(' ');
                            }

                            _result.Append('+');
                            AppendEscapedString(option, quoteWhiteSpace: false);
                            counter++;
                        }
                        _result.Append(')');
                    }
                }

                _result.Append(')');
            }

            /// <summary>
            /// Append a clause to boost an exact matched package ID.
            /// </summary>
            /// <param name="packageId">The package ID that must can be matched.</param>
            /// <param name="boost">The boost for the document with the matching package ID.</param>
            public void AppendExactMatchPackageIdBoost(string packageId, float boost)
            {
                ValidateAdditionalClausesOrThrow(1);
                ValidateTermsOrThrow(new[] { packageId });

                AppendSpaceIfNotEmpty();

                _result.Append(IndexFields.PackageId);
                _result.Append(":");
                AppendEscapedString(packageId, quoteWhiteSpace: true);
                _result.Append("^");
                _result.Append(boost);
            }

            /// <summary>
            /// Append a term to the query that is scoped to a specified field. This generates
            /// queries like "field:value". Unlike <see cref="AppendScopedTerms"/>, this supports
            /// prefix matching.
            /// </summary>
            /// <param name="fieldName">The field that should contain this term.</param>
            /// <param name="term">The term to search.</param>
            /// <param name="required">Whether search results MUST match this term.</param>
            /// <param name="prefixSearch">Whether prefix matches are allowed.</param>
            /// <param name="boost">The boost to results that match this term.</param>
            public void AppendTerm(
                string fieldName,
                string term,
                Operator op = Operator.None,
                bool prefixSearch = false,
                double boost = 1.0)
            {
                // We will generate a single clause.
                ValidateAdditionalClausesOrThrow(1);
                ValidateTermOrThrow(term);

                AppendSpaceIfNotEmpty();

                switch (op)
                {
                    case Operator.Required:
                        _result.Append('+');
                        break;
                    case Operator.Prohibit:
                        _result.Append('-');
                        break;
                }

                if (fieldName != null)
                {
                    _result.Append(fieldName);
                    _result.Append(':');
                }

                // Don't escape whitespace with quotes if this is prefix matching.
                AppendEscapedString(term.Trim(), quoteWhiteSpace: !prefixSearch);

                if (prefixSearch)
                {
                    _result.Append('*');
                }

                if (boost > 1)
                {
                    _result.Append("^");
                    _result.Append(boost);
                }
            }

            /// <summary>
            /// Append search terms to the query that are scoped to a specified field.
            /// This generates queries like "field:(value1 value2)". Unlike
            /// <see cref="AppendTerm"/>, this doesn't support prefix matches.
            /// </summary>
            /// <param name="fieldName">The field that should match the terms.</param>
            /// <param name="terms">The terms to search</param>
            /// <param name="required">Whether search results MUST match these terms.</param>
            public void AppendScopedTerms(
                string fieldName,
                IReadOnlyList<string> terms,
                bool required = false)
            {
                // We will generate a clause for each term and a clause to OR terms together.
                ValidateAdditionalClausesOrThrow(terms.Count + 1);
                ValidateTermsOrThrow(terms);

                AppendSpaceIfNotEmpty();

                if (required)
                {
                    _result.Append('+');
                }

                _result.Append(fieldName);
                _result.Append(":(");

                for (var i = 0; i < terms.Count; i++)
                {
                    if (i > 0)
                    {
                        _result.Append(' ');
                    }

                    AppendEscapedString(terms[i].Trim(), quoteWhiteSpace: true);
                }

                _result.Append(')');
            }

            /// <summary>
            /// Build the Azure Search Query string.
            /// </summary>
            /// <returns>The Azure Search Query string.</returns>
            public override string ToString()
            {
                return _result.ToString();
            }

            private void AppendSpaceIfNotEmpty()
            {
                if (_result.Length > 0)
                {
                    _result.Append(' ');
                }
            }

            /// <summary>
            /// Escapes characters that are special for Azure Search so that the input
            /// results in a single search term.
            /// </summary>
            /// <param name="input">The input to escape.</param>
            /// <param name="quoteWhiteSpace">
            /// If true, the input will be wrapped with quotes if it contains whitespace.
            /// If false, the input's whitespace will be escaped with a backslash.
            /// </param>
            private void AppendEscapedString(string input, bool quoteWhiteSpace)
            {
                var originalLength = _result.Length;

                // Input containing whitespace must be escaped. If quoteWhiteSpace is true, we
                // will wrap the input with quotes. Otherwise, we will escape whitespace characters
                // with backslashes.
                var wrapWithQuotes = quoteWhiteSpace && input.Any(char.IsWhiteSpace);
                if (wrapWithQuotes)
                {
                    _result.Append('"');
                }

                for (var i = 0; i < input.Length; i++)
                {
                    var c = input[i];
                    if (SpecialCharacters.Contains(c) || (!quoteWhiteSpace && char.IsWhiteSpace(c)))
                    {
                        if (originalLength == _result.Length)
                        {
                            _result.Append(input.Substring(0, i));
                        }

                        _result.Append('\\');
                        _result.Append(c);
                    }
                    else if (_result.Length != originalLength)
                    {
                        _result.Append(c);
                    }
                }

                if (wrapWithQuotes)
                {
                    _result.Append('"');
                }

                if (_result.Length == originalLength)
                {
                    _result.Append(input);
                }
            }

            private void ValidateAdditionalClausesOrThrow(int additionalClauses)
            {
                if ((_clauses + additionalClauses) > MaxClauses)
                {
                    throw new InvalidSearchRequestException($"A query can only have up to {MaxClauses} clauses.");
                }

                _clauses += additionalClauses;
            }

            private void ValidateTermsOrThrow(IReadOnlyList<string> terms)
            {
                foreach (var term in terms)
                {
                    ValidateTermOrThrow(term);
                }
            }

            private void ValidateTermOrThrow(string term)
            {
                if (Encoding.Unicode.GetByteCount(term) > MaxTermSizeBytes)
                {
                    throw new InvalidSearchRequestException($"Query terms cannot exceed {MaxTermSizeBytes} bytes.");
                }
            }
        }
    }
}
