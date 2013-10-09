namespace NuGetGallery
{
    public class PackageOwnerConfirmationModel
    {
        public ConfirmOwnershipResult Result { get; set; }
        public string PackageId { get; set; }
        public string Username { get; set; }
    }
}