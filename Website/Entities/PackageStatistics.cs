
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    // IMPORTANT:   Removed the TimeStamp column from this class because 
    //              it's completely tracked by the database layer. Don't
    //              add it back! :) It will be created by the migration.
    public class PackageStatistics : IEntity
    {
        public Package Package { get; set; }
        public int PackageKey { get; set; }
        public string IPAddress { get; set; }
        public string UserAgent { get; set; }
        public int Key { get; set; }
        [StringLength(16)]
        public string Operation { get; set; }
    }
}