// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;
using System;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public class CamelCaseFilter : TokenFilter
    {
        ITermAttribute _termAttribute;
        IOffsetAttribute _offsetAttribute;
        IPositionIncrementAttribute _positionIncrementAttribute;

        Queue<TokenAttributes> _queue = new Queue<TokenAttributes>();

        public CamelCaseFilter(TokenStream stream)
            : base(stream)
        {
            _termAttribute = AddAttribute<ITermAttribute>();
            _offsetAttribute = AddAttribute<IOffsetAttribute>();
            _positionIncrementAttribute = AddAttribute<IPositionIncrementAttribute>();
        }

        public override bool IncrementToken()
        {
            if (_queue.Count > 0)
            {
                SetAttributes(_queue.Dequeue());
                return true;
            }

            if (!input.IncrementToken())
            {
                return false;
            }

            _queue.Enqueue(new TokenAttributes
            {
                TermBuffer = _termAttribute.Term,
                StartOffset = _offsetAttribute.StartOffset,
                EndOffset = _offsetAttribute.EndOffset,
                PositionIncrement = _positionIncrementAttribute.PositionIncrement
            });

            string term = _termAttribute.Term;
            int start = _offsetAttribute.StartOffset;
            int prevStart = start;
            int positionIncrement = 0;
            string prev = string.Empty;

            foreach (string subTerm in TokenizingHelper.CamelCaseSplit(term))
            {
                if (prev != string.Empty)
                {
                    string shingle = string.Format("{0}{1}", prev, subTerm);

                    if (shingle != term)
                    {
                        _queue.Enqueue(new TokenAttributes
                        {
                            TermBuffer = shingle,
                            StartOffset = prevStart,
                            EndOffset = prevStart + shingle.Length,
                            PositionIncrement = 0
                        });
                    }
                }

                if (subTerm != term)
                {
                    _queue.Enqueue(new TokenAttributes
                    {
                        TermBuffer = subTerm,
                        StartOffset = start,
                        EndOffset = start + subTerm.Length,
                        PositionIncrement = positionIncrement
                    });
                }

                positionIncrement = 1;
                prevStart = start;
                start += subTerm.Length;
                prev = subTerm;
            }

            if (_queue.Count > 0)
            {
                SetAttributes(_queue.Dequeue());
                return true;
            }

            return false;
        }

        private void SetAttributes(TokenAttributes next)
        {
            _termAttribute.SetTermBuffer(next.TermBuffer);
            _offsetAttribute.SetOffset(next.StartOffset, next.EndOffset);
            _positionIncrementAttribute.PositionIncrement = next.PositionIncrement;
        }

        private class TokenAttributes
        {
            public string TermBuffer { get; set; }
            public int StartOffset { get; set; }
            public int EndOffset { get; set; }
            public int PositionIncrement { get; set; }
        }
    }
}
