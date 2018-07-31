using Microsoft.Extensions.Logging;

namespace StatusAggregator
{
    public static class LogEvents
    {
        public static EventId RegexFailure = new EventId(400, "Failed to parse incident using Regex.");
    }
}
