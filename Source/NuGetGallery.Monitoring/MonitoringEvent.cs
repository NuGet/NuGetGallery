using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Monitoring
{
    public abstract class MonitoringEvent
    {
        /// <summary>
        /// Gets the type of the event
        /// </summary>
        public EventType Type { get; private set; }

        /// <summary>
        /// Gets the time at which the event occurred
        /// </summary>
        public DateTime TimestampUtc { get; private set; }
        
        /// <summary>
        /// Gets a message associated with the status
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Gets the resource that was tested in this event
        /// </summary>
        public string Resource { get; private set; }

        protected MonitoringEvent(EventType type, DateTime timestampUtc, string message, string resource)
        {
            Type = type;
            TimestampUtc = timestampUtc;
            Message = message;
            Resource = resource;
        }
    }

    public class MonitoringMessageEvent : MonitoringEvent {
        public MonitoringMessageEvent(EventType type, DateTime timestampUtc, string message, string resource) :
            base(type, timestampUtc, message, resource) {}
    }

    public class MonitoringQoSTimeEvent : MonitoringEvent
    {
        /// <summary>
        /// Gets the time taken by the action described by this event
        /// </summary>
        public TimeSpan TimeTaken { get; private set; }

        public MonitoringQoSTimeEvent(TimeSpan timeTaken, DateTime timestampUtc, string message, string resource)
            : base(EventType.QualityOfService, timestampUtc, message, resource) {
                TimeTaken = timeTaken;
        }
    }

    public class MonitoringQoSNumberEvent : MonitoringEvent
    {
        /// <summary>
        /// Gets the value used as a quality-of-service indicator for the action described by this event.
        /// </summary>
        public int Value { get; private set; }

        public MonitoringQoSNumberEvent(int value, DateTime timestampUtc, string message, string resource)
            : base(EventType.QualityOfService, timestampUtc, message, resource)
        {
            Value = value;
        }
    }
}
