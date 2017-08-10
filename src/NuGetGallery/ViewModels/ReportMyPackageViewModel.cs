using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using NuGetGallery.Infrastructure;

namespace NuGetGallery
{
    public class ReportMyPackageViewModel
    {
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }

        [NotEqual(ReportPackageReason.HasABugOrFailedToInstall, ErrorMessage = "Unfortunately we cannot provide support for bugs in NuGet Packages. Please contact owner(s) for assistance.")]
        [Required(ErrorMessage = "You must select a reason for reporting the package.")]
        [Display(Name = "Reason")]
        public ReportPackageReason? Reason { get; set; }

        [Display(Name = "Send me a copy")]
        public bool CopySender { get; set; }

        [Required(ErrorMessage = "Please enter a message.")]
        [AllowHtml]
        [StringLength(4000)]
        [Display(Name = "Details")]
        public string Message { get; set; }

        public bool ConfirmedUser { get; set; }

        public IEnumerable<ReportPackageReason> ReasonChoices { get; set; }

        public ReportMyPackageViewModel()
        {
        }
    }
}