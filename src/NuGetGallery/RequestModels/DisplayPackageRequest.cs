using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class DisplayPackageRequest
    {
        [Required]
        public string Id { get; set; }

        public string Version { get; set; }
    }
}