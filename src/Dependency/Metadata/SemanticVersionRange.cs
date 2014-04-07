
using System.Collections;
namespace Resolver.Metadata
{
    public class SemanticVersionRange
    {
        public static SemanticVersionComparer DefaultComparer = new SemanticVersionComparer();

        public SemanticVersion Lower { get; private set; }
        public bool LowerIsInclusive { get; private set; }

        public SemanticVersion Upper { get; private set; }
        public bool UpperIsInclusive { get; private set; }

        public bool Latest { get; private set; }

        public SemanticVersionRange(SemanticVersion lower, bool lowerIsInclusive, SemanticVersion upper, bool upperIsInclusive)
        {
            Lower = lower;
            LowerIsInclusive = lowerIsInclusive;
            Upper = upper;
            UpperIsInclusive = upperIsInclusive;
        }

        public SemanticVersionRange(SemanticVersion lower, SemanticVersion upper)
            : this(lower, true, upper, true)
        {
        }

        //TODO: remove this
        public SemanticVersionRange(SemanticVersion lower, bool lowerIsInclusive)
            : this(lower, lowerIsInclusive, null, false)
        {
        }

        public SemanticVersionRange()
            : this(null, false, null, false)
        {
            Latest = true;
        }

        public SemanticVersionRange(SemanticVersion current, SemanticVersionSpan span)
        {
            //TODO: just handle MaxMinor for the moment

            if (span.Span == SemanticVersionSpan.SpanType.MaxMinorSpan)
            {
                SemanticVersion other = new SemanticVersion(current.Major + 1);

                Lower = current;
                LowerIsInclusive = true;
                Upper = other;
                UpperIsInclusive = false;
            }
            else
            {
                SemanticVersion other = new SemanticVersion(
                    current.Major + span.Major,
                    current.Minor + span.Minor,
                    current.Patch + span.Patch);

                if (DefaultComparer.Compare(current, other) == 0)
                {
                    Lower = current;
                    LowerIsInclusive = true;
                    Upper = current;
                    UpperIsInclusive = true;
                }
                else if (DefaultComparer.Compare(current, other) > 0)
                {
                    Lower = other;
                    LowerIsInclusive = true;
                    Upper = current;
                    UpperIsInclusive = true;
                }
                else
                {
                    Lower = current;
                    LowerIsInclusive = true;
                    Upper = other;
                    UpperIsInclusive = true;
                }
            }
        }

        public bool Includes(SemanticVersion version, IComparer comparer = null)
        {
            if (comparer == null)
            {
                comparer = DefaultComparer;
            }

            int lowerComparison = comparer.Compare(Lower, version);
            int upperComparison = comparer.Compare(Upper, version);

            bool lowerCheck = (lowerComparison < 0) || (lowerComparison == 0 && LowerIsInclusive);
            bool upperCheck = (upperComparison > 0) || (upperComparison == 0 && UpperIsInclusive);

            return (lowerCheck && upperCheck);
        }

        public override string ToString()
        {
            if (Latest)
            {
                return string.Empty;
            }
            else if (Upper == null || DefaultComparer.Compare(Lower, Upper) == 0)
            {
                if (LowerIsInclusive && UpperIsInclusive)
                {
                    return string.Format("[{0}]", Lower);
                }
                else
                {
                    return Lower.ToString();
                }
            }
            else
            {
                return string.Format("{0}{1},{2}{3}", LowerIsInclusive ? "[" : "(", Lower == null ? string.Empty : Lower.ToString(), Upper, UpperIsInclusive ? "]" : ")");
            }
        }

        public static SemanticVersionRange Parse(string range)
        {
            if (string.IsNullOrEmpty(range))
            {
                return new SemanticVersionRange();
            }

            string[] fields = range.Split(',');
            if (fields.Length == 1)
            {
                bool lowerIsInclusive = true;
                bool upperIsInclusive = true;

                string s = fields[0];

                if (s.StartsWith("("))
                {
                    lowerIsInclusive = false;
                    s = s.Substring(1);
                }
                else if (s.StartsWith("["))
                {
                    lowerIsInclusive = true;
                    s = s.Substring(1);
                }

                if (s.EndsWith(")"))
                {
                    upperIsInclusive = false;
                    s = s.Substring(0, s.Length - 1);
                }
                else if (s.EndsWith("]"))
                {
                    upperIsInclusive = true;
                    s = s.Substring(0, s.Length - 1);
                }

                SemanticVersion version = SemanticVersion.Parse(s); 

                return new SemanticVersionRange(version, lowerIsInclusive, version, upperIsInclusive);
            }
            else
            {
                bool lowerIsInclusive = true;
                bool upperIsInclusive = false;

                string lower = fields[0];
                string upper = fields[1];

                if (lower.StartsWith("("))
                {
                    lowerIsInclusive = false;
                    lower = lower.Substring(1);
                }
                else if (lower.StartsWith("["))
                {
                    lowerIsInclusive = true;
                    lower = lower.Substring(1);
                }

                if (upper.EndsWith(")"))
                {
                    upperIsInclusive = false;
                    upper = upper.Substring(0, upper.Length - 1);
                }
                else if (upper.EndsWith("]"))
                {
                    upperIsInclusive = true;
                    upper = upper.Substring(0, upper.Length - 1);
                }

                SemanticVersion lowerVersion = SemanticVersion.Parse(lower);
                SemanticVersion upperVersion = SemanticVersion.Parse(upper);

                return new SemanticVersionRange(lowerVersion, lowerIsInclusive, upperVersion, upperIsInclusive);
            }
        }
    }
}
