using Lucene.Net.Search;

namespace NuGetGallery
{
    public class CustomSimilarity : DefaultSimilarity
    {
        public override float LengthNorm(string fieldName, int numTerms)
        {
            if (fieldName == "TokenizedId" || fieldName == "ShingledId" || fieldName == "Owners")
            {
                return 1;
            }
            else if (fieldName == "Tags" && numTerms <= 9)
            {
                return 1;
            }
            else
            {
                return base.LengthNorm(fieldName, numTerms);
            }
        }
    }
}
