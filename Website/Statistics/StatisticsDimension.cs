using System;
using Microsoft.Internal.Web.Utils;

namespace NuGetGallery.Statistics
{
    public class StatisticsDimension
    {
        public string Value { get; set; }
        public string DisplayName { get; set; }
        public bool IsChecked { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as StatisticsDimension;
            return other != null &&
                String.Equals(Value, other.Value, StringComparison.Ordinal) &&
                String.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal) &&
                IsChecked == other.IsChecked;
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .Add(Value)
                .Add(DisplayName)
                .Add(IsChecked)
                .CombinedHash;
        }
    }
}