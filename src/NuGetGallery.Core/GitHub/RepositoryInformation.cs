using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace NuGetGallery.GitHub
{
    public class RepositoryInformation : IEquatable<RepositoryInformation>, IComparable<RepositoryInformation>
    {
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

        public List<string> Dependencies { get; set; } = null;

        public RepositoryInformation()
        { }

        public RepositoryInformation(string owner, string repoName, string cloneUrl, int starCount, List<string> dependencies)
        {
            Owner = owner;
            Name = repoName;
            Url = cloneUrl;
            Stars = starCount;
            Dependencies = dependencies;
        }

        public override bool Equals(object obj)
        {
            return obj is RepositoryInformation information && Equals(information);
        }

        public bool Equals(RepositoryInformation other)
        {
            return Url.Equals(other.Url, StringComparison.InvariantCultureIgnoreCase);
        }
        public override int GetHashCode()
        {
            // Using toLower() to make the hash case insensitive
            return Url.ToLower().GetHashCode();
        }

        public int CompareTo(RepositoryInformation other)
        {
            return Stars.CompareTo(other.Stars);
        }
    }
}
