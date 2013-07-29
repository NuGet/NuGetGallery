using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class ModifyCuratedPackageRequest
    {
        [Required]
        public bool Included { get; set; }
    }
}