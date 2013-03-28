using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public enum ReportPackageReason
    {
        Other,
        HasABug,
        ContainsMaliciousCode,
        ViolatesALicenseIOwn,
        IsFraudulent,
        ContainsPrivateAndConfidentialData,
        PublishedWithWrongVersion,
        ReleasedInPublicByAccident,
    }

    public class ReportAbuseViewModel
    {
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }

        [Display(Name = "Contacted Owner")]
        public bool AlreadyContactedOwner { get; set; }

        [Required(ErrorMessage = "You must select a reason for reporting the package")]
        [Display(Name = "Reason")]
        public string Reason { get; set; }

        [Required]
        [StringLength(4000)]
        [Display(Name = "Abuse Report")]
        public string Message { get; set; }

        [Required(ErrorMessage = "Please enter your email address.")]
        [StringLength(4000)]
        [Display(Name = "Your Email Address")]
        [DataType(DataType.EmailAddress)]
        [RegularExpression(
            @"(?i)^(?!\.)(""([^""\r\\]|\\[""\r\\])*""|([-a-z0-9!#$%&'*+/=?^_`{|}~]|(?<!\.)\.)*)(?<!\.)@[a-z0-9][\w\.-]*[a-z0-9]\.[a-z][a-z\.]*[a-z]$",
            ErrorMessage = "This doesn't appear to be a valid email address.")]
        public string Email { get; set; }

        public bool ConfirmedUser { get; set; }

        public ICollection<ReportPackageReason> ReasonChoices { get; private set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly Dictionary<ReportPackageReason, string> ReasonDescriptions = new Dictionary<ReportPackageReason, string>
        {
            {ReportPackageReason.Other, "Other" },
            {ReportPackageReason.HasABug, "The package has a bug" },
            {ReportPackageReason.ContainsMaliciousCode, "The package contains malicious code" },
            {ReportPackageReason.ViolatesALicenseIOwn, "The package violates a license I own" },
            {ReportPackageReason.IsFraudulent, "The package owner is fraudulently claiming authorship" },
            {ReportPackageReason.ContainsPrivateAndConfidentialData, "The package contains private/confidential data" },
            {ReportPackageReason.PublishedWithWrongVersion, "The package was published as the wrong version" },
            {ReportPackageReason.ReleasedInPublicByAccident, "The package was not intended to be published publically on nuget.org"},
        };

        public ReportAbuseViewModel()
        {
            ReasonChoices = new Collection<ReportPackageReason>();
        }
    }
}