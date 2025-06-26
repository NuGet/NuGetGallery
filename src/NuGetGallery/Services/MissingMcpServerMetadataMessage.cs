namespace NuGetGallery
{
    /// <summary>
    /// Represents an MCP Server package missing a server.json file.
    /// </summary>
    public class MissingMcpServerMetadataMessage : IValidationMessage
    {
        public bool HasRawHtmlRepresentation => true;
        public string PlainTextMessage => Strings.UploadPackage_MissingMcpServerMetadata;
        public string RawHtmlMessage => Strings.UploadPackage_MissingMcpServerMetadataHtml;
    }
}
