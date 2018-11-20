using Microsoft.Extensions.Logging;

namespace StatusAggregator
{
    public static class LogEvents
    {
        public static EventId RegexFailure = new EventId(400, "Failed to parse incident using Regex.");
        public static EventId ManualChangeFailure = new EventId(401, "Failed to apply a manual change.");
        public static EventId IncidentIngestionFailure = new EventId(402, "Failed to update incident API data.");
    }
}
