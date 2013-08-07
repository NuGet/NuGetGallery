using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Web;

namespace NuGetGallery.Infrastructure
{
    /// <summary>
    /// Used by EF Migrations to load the Entity Context for migrations and such like.
    /// Don't use it for anything else because it doesn't respect read-only mode.
    /// </summary>
    public class EntitiesContextFactory : IDbContextFactory<EntitiesContext>
    {
        public EntitiesContext Create()
        {
            return new EntitiesContext();
        }
    }
}