using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace NuGetGallery.GitHub
{
    public class RepositoryInformation
    {
        public RepositoryInformation(
            string id,
            string url,
            int stars,
            IReadOnlyList<string> dependencies)
        {
            if (stars < 0)
            {
                throw new IndexOutOfRangeException(string.Format("{0} cannot have a negative value!", nameof(stars)));
            }

            Id = id;
            var idSplit = Id.Split('/');
            if (idSplit.Length == 2)
            {
                Owner = idSplit[0];
                Name = idSplit[1];
            }
            else
            {
                throw new ArgumentException(string.Format("{0} has an invalid format! It should be \"owner/repositoryName\", instead it is: {1}", nameof(Id), Id));
            }

            Url = url ?? throw new ArgumentNullException(nameof(url));
            Stars = stars;
            Dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        }

        [JsonIgnore]
        public string Name { get; }
        [JsonIgnore]
        public string Owner { get; }
        public string Url { get; }
        public int Stars { get; }
        public string Id { get; }
        public IReadOnlyList<string> Dependencies { get; }
    }
}
