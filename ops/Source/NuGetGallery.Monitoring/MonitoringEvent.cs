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
        /// Gets a action that occurred to trigger the event.
        /// </summary>
        public string Action { get; private set; }

        /// <summary>
        /// Gets the resource that was tested in this event
        /// </summary>
        public string Resource { get; private set; }

        protected MonitoringEvent(EventType type, DateTime timestampUtc, string message, string resource)
        {
            Type = type;
            TimestampUtc = timestampUtc;
            Action = message;
            Resource = resource;
        }
    }

    public class MonitoringMessageEvent : MonitoringEvent {
        public MonitoringMessageEvent(EventType type, DateTime timestampUtc, string message, string resource) :
            base(type, timestampUtc, message, resource) {}
    }

    public class MonitoringQoSEvent : MonitoringEvent
    {
        public bool Success { get; private set; }
        public object Value { get; private set; }

        public MonitoringQoSEvent(bool success, object value, DateTime timestampUtc, string message, string resource)
            : base(EventType.QualityOfService, timestampUtc, message, resource)
        {
            Success = success;
            Value = value;
        }
    }
}
