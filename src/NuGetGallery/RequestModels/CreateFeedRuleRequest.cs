using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class CreateFeedRuleRequest
    {
        [Required]
        [DisplayName("Package ID")]
        public string PackageId { get; set; }

        [DisplayName("Package Version Range")]
        public string PackageVersionSpec { get; set; }

        [DataType(DataType.MultilineText)]
        public string Notes { get; set; }
    }
}