namespace NuGetGallery.Data
{
    public class EntitiesContextFactory : IEntitiesContextFactory
    {
        public IDbModelManager ModelManager { get; protected set; }
        public IConfiguration Configuration { get; protected set; }

        protected EntitiesContextFactory()
        {
        }

        public EntitiesContextFactory(IDbModelManager modelManager, IConfiguration configuration) : this()
        {
            ModelManager = modelManager;
            Configuration = configuration;
        }

        public EntitiesContext Create(bool readOnly)
        {
            // Disable the warning that tells us to use EntitiesContextFactory because we're IN EntitiesContextFactory :)
#pragma warning disable 618
            return new EntitiesContext(
                Configuration.SqlConnectionString,
                ModelManager.GetCurrentModel(),
                readOnly);
#pragma warning restore 618
        }
    }
}