
using System.Text.RegularExpressions;

namespace NuGetGallery {
    public class SubmitPackageViewModel : IPackageVersionModel {
        public string Id { get; set; }
        public string Version { get; set; }

        public string Authors { get; set; }
        public string LicenseUrl { get; set; }
        public string ProjectUrl { get; set; }
        public bool RequiresLicenseAcceptance { get; set; }
        public string Summary { get; set; }
        public string Tags { get; set; }
        public string Title { get; set; }

        private string _description;
        public string Description {
            get {
                return _description;
            }
            set {
                value = FormatDescription(value);
                _description = value;
            }
        }

        private string FormatDescription(string value) {
            if (string.IsNullOrWhiteSpace(value)) return "";

            var signature = string.Copy(value ?? "");
            signature = Regex.Replace(signature, @"\r\n", string.Format("<br{0}", " />"));
            signature = Regex.Replace(signature, @"\r", string.Format("<br{0}", " />"));
            signature = Regex.Replace(signature, @"\n", string.Format("<br{0}", " />"));
            return signature;
        }
    }
}