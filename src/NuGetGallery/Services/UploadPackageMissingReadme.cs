namespace NuGetGallery
{
    /// <summary>
    /// Represents a package ID reservation conflict
    /// </summary>
    public class UploadPackageMissingReadme : IValidationMessage
    {
        public bool HasRawHtmlRepresentation => true;
        public string PlainTextMessage => Strings.UploadPackage_MissingReadme;
        public string RawHtmlMessage => Strings.UploadPackage_MissingReadmeHtml;
    }
}