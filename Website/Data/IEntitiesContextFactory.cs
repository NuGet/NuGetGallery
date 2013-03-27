using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Data.Model;

namespace NuGetGallery.Data
{
    public interface IEntitiesContextFactory
    {
        // Things that use this interface need the concrete DbContext version of the type
        EntitiesContext Create(bool readOnly);
    }
}
