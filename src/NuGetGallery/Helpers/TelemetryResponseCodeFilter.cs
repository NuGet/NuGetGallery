using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;

namespace NuGetGallery.Helpers
{
    public class TelemetryResponseCodeFilter : ITelemetryProcessor
    {
        public TelemetryResponseCodeFilter(ITelemetryProcessor next)
        {
            Next = next;
        }
        
        private ITelemetryProcessor Next { get; set; }

        public void Process(ITelemetry item)
        {
            var request = item as RequestTelemetry;
            int responseCode;

            if (request != null && int.TryParse(request.ResponseCode, out responseCode))
            {
                if (responseCode == 400 || responseCode == 404)
                {
                    request.Success = true;
                }
            }

            this.Next.Process(item);
        }
    }
}