using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class SourceRepositoryViewModel
    {
        public string Origin { get; set; }
        public string AvatarUrl { get; set; }
        public string Owner { get; set; }
        public string Name { get; set; }
        public string URL { get; set; }
        public string ReadmeHTML { get; set; }
        public string ProgrammingLanguage { get; set; }
        public int StarCount { get; set; }
        public int ForkCount { get; set; }
        public int OpenIssueCount { get; set; }
        public int FollowersCount { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
    }
}