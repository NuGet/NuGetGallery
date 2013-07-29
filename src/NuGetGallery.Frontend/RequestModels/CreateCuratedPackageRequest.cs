using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class CreateCuratedPackageRequest
    {
        [Required]
        [DisplayName("Package ID")]
        public string PackageId { get; set; }

        [DataType(DataType.MultilineText)]
        public string Notes { get; set; }
    }
}