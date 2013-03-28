using System;

namespace NuGetGallery.Data
{
    public class DatabaseVersion
    {
        public string Id { get; private set; }
        public DateTime CreatedUtc { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }

        public DatabaseVersion(string id, DateTime createdUtc, string name, string description)
        {
            Id = id;
            CreatedUtc = createdUtc;
            Name = name;
            Description = description;
        }
    }
}