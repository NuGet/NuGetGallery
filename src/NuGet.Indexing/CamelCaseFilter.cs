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

        Queue<Tuple<string, int, int, int>> _queue = new Queue<Tuple<string, int, int, int>>();

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

            string term = _termAttribute.Term;
            int start = _offsetAttribute.StartOffset;
            int prevStart = start;
            int positionIncrement = _positionIncrementAttribute.PositionIncrement;
            string prev = string.Empty;

            foreach (string subTerm in TokenizingHelper.CamelCaseSplit(term))
            {
                if (prev != string.Empty)
                {
                    string shingle = string.Format("{0}{1}", prev, subTerm);
                    _queue.Enqueue(new Tuple<string, int, int, int>(shingle, prevStart, prevStart + shingle.Length, 0));
                }

                _queue.Enqueue(new Tuple<string, int, int, int>(subTerm, start, start + subTerm.Length, positionIncrement));
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

        private void SetAttributes(Tuple<string, int, int, int> next)
        {
            _termAttribute.SetTermBuffer(next.Item1);
            _offsetAttribute.SetOffset(next.Item2, next.Item3);
            _positionIncrementAttribute.PositionIncrement = next.Item4;
        }
    }
}
