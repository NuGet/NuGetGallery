using System.ComponentModel.DataAnnotations;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery {
    public class SubmitPackageRequest {
        [Required]
        [AdditionalMetadata("Hint", "Your package file will be uploaded and hosted on the gallery server.")]
        public HttpPostedFile PackageFile { get; set; }
    }
}