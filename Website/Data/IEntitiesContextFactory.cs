namespace NuGetGallery.Data
{
    public interface IEntitiesContextFactory
    {
        // Things that use this interface need the concrete DbContext version of the type
        EntitiesContext Create(bool readOnly);
    }
}
