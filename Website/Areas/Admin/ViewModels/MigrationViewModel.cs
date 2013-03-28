using System;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class MigrationViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string Description { get; set; }
        public DateTime CreatedLocal { get { return CreatedUtc.ToLocalTime(); } }
    }
}