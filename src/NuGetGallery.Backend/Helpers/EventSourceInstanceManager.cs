using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Backend.Helpers
{
    internal static class EventSourceInstanceManager
    {
        private static Dictionary<Type, EventSource> _instances = new Dictionary<Type, EventSource>();

        public static TEventSource Get<TEventSource>()
            where TEventSource : EventSource
        {
            return (TEventSource)Get(typeof(TEventSource));
        }

        public static EventSource Get(Type eventSourceType)
        {
            EventSource eventSource;
            if (!_instances.TryGetValue(eventSourceType, out eventSource))
            {
                var field = eventSourceType.GetField("Log", BindingFlags.Public | BindingFlags.Static);
                if (field != null && eventSourceType.IsAssignableFrom(field.FieldType))
                {
                    eventSource = (EventSource)field.GetValue(null);
                }
                else
                {
                    var ctor = eventSourceType.GetConstructor(Type.EmptyTypes);
                    if (ctor != null)
                    {
                        eventSource = (EventSource)ctor.Invoke(new object[0]);
                    }
                    else
                    {
                        eventSource = null;
                    }
                }
                _instances[eventSourceType] = eventSource;
            }
            return eventSource;
        }
    }
}
