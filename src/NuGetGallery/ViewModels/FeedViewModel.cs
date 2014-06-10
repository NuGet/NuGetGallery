using System.Collections.Generic;

namespace NuGetGallery
{
    public class FeedViewModel
    {
        public string Name { get; set; }
        public List<string> Managers { get; set; }
        public bool Inclusive { get; set; }
        public List<FeedRuleDesc> FeedRules { get; set; }

        public class FeedRuleDesc
        {
            public string Id { get; set; }
            public string VersionSpec { get; set; }
            public string Notes { get; set; }

            public FeedRuleDesc(FeedRule rule)
            {
                Id = rule.PackageRegistration.Id;
                VersionSpec = rule.PackageVersionSpec;

                if (rule.Notes != null && rule.Notes.Length > 40)
                {
                    Notes = rule.Notes.Substring(0, 37) + "...";
                }
                else
                {
                    Notes = rule.Notes;
                }
            }
        }
    }
}