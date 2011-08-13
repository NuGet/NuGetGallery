using System.ComponentModel.DataAnnotations;

namespace NuGetGallery {
    public class ReportAbuseViewModel {
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }

        [Required(ErrorMessage = "Please enter a detailed abuse report.")]
        [StringLength(4000)]
        public string Message { get; set; }

        [Required(ErrorMessage = "Please enter your email address.")]
        [StringLength(4000)]
        public string Email { get; set; }
    }
}