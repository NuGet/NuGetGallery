namespace NuGetGallery
{
    /// <summary>
    /// Represents package missing an embedded README.
    /// </summary>
    public class UploadPackageMissingReadme : IValidationMessage
    {
        public bool HasRawHtmlRepresentation => true;
        public string PlainTextMessage => Strings.UploadPackage_MissingReadme;
        public string RawHtmlMessage => Strings.UploadPackage_MissingReadmeHtml;
    }
}