using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class CreatedCuratedPackageRequest
    {   
        [Required]
        [DisplayName("Package ID")]
        public string PackageId { get; set; }
        
        [DataType(DataType.MultilineText)]
        public string Notes { get; set; }
    }
}