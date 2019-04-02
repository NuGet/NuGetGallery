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
        private class AzureSearchQueryBuilder
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

            public AzureSearchQueryBuilder()
            {
                _result = new StringBuilder();
                _clauses = 0;
            }

            /// <summary>
            /// Append unscoped search terms to the query. This can be called many times.
            /// </summary>
            /// <param name="terms">
            /// The terms to append to the search query. Each term will be escaped and will be wrapped
            /// with quotes if it contains whitespaces.
            /// </param>
            public void AppendTerms(IReadOnlyList<string> terms)
            {
                ValidateOrThrow(terms, additionalClauses: terms.Count);

                for (var i = 0; i < terms.Count; i++)
                {
                    if (_result.Length > 0)
                    {
                        _result.Append(' ');
                    }

                    AppendEscapedString(terms[i], quoteWhiteSpace: true);
                }
            }

            /// <summary>
            /// Append search terms to the query. These terms will be scoped to the specified field. This can be called many times.
            /// </summary>
            /// <param name="fieldName">The field that these terms should be scoped to.</param>
            /// <param name="terms">The terms to search for.</param>
            /// <param name="required">Whether search results MUST match these terms.</param>
            /// <param name="prefixSearch">If true, prefix matches are allowed for the terms.</param>
            public void AppendScopedTerms(
                string fieldName,
                IReadOnlyList<string> terms,
                bool required = false,
                bool prefixSearch = false)
            {
                // We will only generate a single clause if this field-scope has a single term.
                // Otherwise, we will generate a clause for each term and a clause to OR terms together.
                var additionalClauses = 1;
                if (terms.Count > 1)
                {
                    additionalClauses += terms.Count;
                }

                ValidateOrThrow(terms, additionalClauses);

                if (_result.Length > 0)
                {
                    _result.Append(' ');
                }

                if (required)
                {
                    _result.Append('+');
                }

                _result.Append(fieldName);
                _result.Append(':');

                if (terms.Count == 1)
                {
                    AppendScopedTerm(terms[0], prefixSearch);
                }
                else
                {
                    _result.Append('(');

                    for (var i = 0; i < terms.Count; i++)
                    {
                        if (i > 0)
                        {
                            _result.Append(' ');
                        }

                        AppendScopedTerm(terms[i], prefixSearch);
                    }

                    _result.Append(')');
                }
            }

            /// <summary>
            /// Build the Azure Search Query string.
            /// </summary>
            /// <returns>The Azure Search Query string.</returns>
            public override string ToString()
            {
                return _result.ToString();
            }

            private void AppendScopedTerm(string term, bool prefixSearch)
            {
                AppendEscapedString(term.Trim(), quoteWhiteSpace: !prefixSearch);

                if (prefixSearch)
                {
                    _result.Append('*');
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

            private void ValidateOrThrow(IReadOnlyList<string> terms, int additionalClauses)
            {
                if ((_clauses + additionalClauses) > MaxClauses)
                {
                    throw new InvalidSearchRequestException($"A query can only have up to {MaxClauses} clauses");
                }

                if (terms.Any(TermExceedsMaxSize))
                {
                    throw new InvalidSearchRequestException($"Query terms cannot exceed {MaxTermSizeBytes} bytes");
                }

                _clauses += additionalClauses;
            }

            private static bool TermExceedsMaxSize(string term)
            {
                return Encoding.Unicode.GetByteCount(term) > MaxTermSizeBytes;
            }
        }
    }
}
