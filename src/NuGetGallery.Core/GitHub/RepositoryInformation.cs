using Newtonsoft.Json;
using System.Collections.Generic;

namespace NuGetGallery.GitHub
{
    public class RepositoryInformation
    {
        public RepositoryInformation()
        { }

        public RepositoryInformation(
            string owner,
            string repoName,
            string cloneUrl,
            int starCount,
            IReadOnlyList<string> dependencies)
        {
            if(starCount < 0)
            {
                throw new System.IndexOutOfRangeException(string.Format("{0} cannot have a negative value!", nameof(starCount)));
            }

            Owner = owner ?? throw new System.ArgumentException(string.Format("{0} cannot be null!", nameof(owner)));
            Name = repoName ?? throw new System.ArgumentException(string.Format("{0} cannot be null!", nameof(repoName)));;
            Url = cloneUrl ?? throw new System.ArgumentException(string.Format("{0} cannot be null!", nameof(cloneUrl)));;
            Stars = starCount;
            Dependencies = dependencies ?? throw new System.ArgumentException(string.Format("{0} cannot be null!", nameof(dependencies)));;
        }

        [JsonIgnore]
        public string Name { get; set; }
        [JsonIgnore]
        public string Owner { get; set; }
        public string Url { get; set; }
        public int Stars { get; set; }
        public string Id
        {
            get => Owner + "/" + Name; set
            {
                var split = value.Split('/');
                if (split.Length == 2)
                {
                    Owner = split[0];
                    Name = split[1];
                }
            }
        }

        public IReadOnlyList<string> Dependencies { get; set; } = null;
    }
}
