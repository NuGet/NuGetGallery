using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.Diagnostics
{
    public class DiagnosticsServiceFacts
    {
        public class TheGetSourceMethod
        {
            [Fact]
            public void RequiresNonNullOrEmptyName()
            {
                ContractAssert.ThrowsArgNullOrEmpty(s => new DiagnosticsService().GetSource(s), "name");
            }
        }
    }
}
