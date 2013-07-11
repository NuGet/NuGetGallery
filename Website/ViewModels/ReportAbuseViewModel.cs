using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using NuGetGallery.Infrastructure;

namespace NuGetGallery
{
    public enum ReportPackageReason
    {
        [Description("Other")]
        Other,

        [Description("The package has a bug")]
        HasABug,

        [Description("The package contains malicious code")]
        ContainsMaliciousCode,

        [Description("The package violates a license I own")]
        ViolatesALicenseIOwn,

        [Description("The package owner is fraudulently claiming authorship")]
        IsFraudulent,

        [Description("The package contains private/confidential data")]
        ContainsPrivateAndConfidentialData,

        [Description("The package was published as the wrong version")]
        PublishedWithWrongVersion,

        [Description("The package was not intended to be published publically on nuget.org")]
        ReleasedInPublicByAccident,
    }

    public class ReportAbuseViewModel
    {
        public static readonly string NoReasonSpecifiedText = "Select a reason";

        public string PackageId { get; set; }
        public string PackageVersion { get; set; }

        [Display(Name = "Contacted Owner")]
        public bool AlreadyContactedOwner { get; set; }

        [NotEqual(ReportPackageReason.HasABug, ErrorMessage = "Unfortunately we cannot provide support for bugs in NuGet Packages. You should contact the owner(s) for assistance.")]
        [Required(ErrorMessage = "You must select a reason for reporting the package")]
        [Display(Name = "Reason")]
        public ReportPackageReason? Reason { get; set; }

        [Required]
        [StringLength(4000)]
        [Display(Name = "Abuse Report")]
        public string Message { get; set; }

        [Required(ErrorMessage = "Please enter your email address.")]
        [StringLength(4000)]
        [Display(Name = "Your Email Address")]
        //[DataType(DataType.EmailAddress)] - does not work with client side validation
        [RegularExpression(RegisterRequest.EmailValidationRegex,
            ErrorMessage = "This doesn't appear to be a valid email address.")]
        public string Email { get; set; }

        public bool ConfirmedUser { get; set; }

        public IEnumerable<ReportPackageReason> ReasonChoices { get; set; }

        public ReportAbuseViewModel()
        {
        }
    }
}