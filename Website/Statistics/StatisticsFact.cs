using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Internal.Web.Utils;

namespace NuGetGallery.Statistics
{
    public class StatisticsFact
    {
        public StatisticsFact(IDictionary<string, string> dimensions, int amount)
        {
            Dimensions = new Dictionary<string, string>(dimensions);
            Amount = amount;
        }

        public IDictionary<string, string> Dimensions { get; private set; }
        public int Amount { get; private set; }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            foreach (var pair in Dimensions)
            {
                builder.Append(pair.Key + "=" + pair.Value + ";");
            }
            builder.Append("Amount=" + Amount);
            return builder.ToString();
        }

        public override bool Equals(object obj)
        {
            var other = obj as StatisticsFact;
            return other != null &&
                Enumerable.SequenceEqual(Dimensions, other.Dimensions) &&
                Amount == other.Amount;
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .Add(Dimensions)
                .Add(Amount)
                .CombinedHash;
        }
    }
}