using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Scaffolding.Internal;
using Microsoft.EntityFrameworkCore.Design;

namespace NuGetGallery
{
    public class CustomCandidateNamingService : CandidateNamingService
    {
        public override string GetDependentEndCandidateNavigationPropertyName(IReadOnlyForeignKey foreignKey)
        {
            Console.WriteLine($"foreign key: {foreignKey.ToString()}");
            return base.GetDependentEndCandidateNavigationPropertyName(foreignKey);
        }
    }
}
