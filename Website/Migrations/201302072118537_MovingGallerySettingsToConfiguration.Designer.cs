// <auto-generated />
namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    using System.Data.Entity.Migrations.Infrastructure;
    
    public sealed partial class MovingGallerySettingsToConfiguration : IMigrationMetadata
    {
        string IMigrationMetadata.Id
        {
            get { return "201302072118537_MovingGallerySettingsToConfiguration"; }
        }
        
        string IMigrationMetadata.Source
        {
            get { return null; }
        }
        
        string IMigrationMetadata.Target
        {
            get { return "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"; }
        }
    }
}
