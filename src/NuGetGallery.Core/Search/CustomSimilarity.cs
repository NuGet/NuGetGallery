using Lucene.Net.Search;

namespace NuGetGallery
{
    public class CustomSimilarity : DefaultSimilarity
    {
        public override float LengthNorm(string fieldName, int numTerms)
        {
            return (fieldName == "TokenizedId" || fieldName == "ShingledId" || fieldName == "Owners") ? 1 : base.LengthNorm(fieldName, numTerms);
        }
    }
}
