using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace NuGetGallery.Services
{
    public class NullTyposquattingService : ITyposquattingService
    {
        public Task<TyposquattingCheckResult> IsUploadedPackageIdTyposquattingAsync(TyposquattingCheckInfo checkInfo)
        {
            return Task.FromResult(new TyposquattingCheckResult(
                wasUploadBlocked: false,
                typosquattingCheckCollisionIds: Enumerable.Empty<string>(),
                telemetryData: new Dictionary<TyposquattingCheckMetrics, object>()));
        }
    }
}