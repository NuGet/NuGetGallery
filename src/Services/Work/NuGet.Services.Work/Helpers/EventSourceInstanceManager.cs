using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Work.Helpers
{
    internal static class EventSourceInstanceManager
    {
        private static ConcurrentDictionary<Type, EventSource> _instances = new ConcurrentDictionary<Type, EventSource>();

        public static TEventSource Get<TEventSource>()
            where TEventSource : EventSource
        {
            return (TEventSource)Get(typeof(TEventSource));
        }

        public static EventSource Get(Type eventSourceType)
        {
            return _instances.GetOrAdd(eventSourceType, type =>
            {
                var field = type.GetField("Log", BindingFlags.Public | BindingFlags.Static);
                EventSource eventSource;
                if (field != null && type.IsAssignableFrom(field.FieldType))
                {
                    eventSource = (EventSource)field.GetValue(null);
                }
                else
                {
                    throw new MissingMemberException(String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.EventSourceInstanceManager_EventSourceDoesNotHaveLogField,
                        eventSourceType.AssemblyQualifiedName));
                }
                return eventSource;
            });
        }
    }
}
