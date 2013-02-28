using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Glimpse.Core.Message;

namespace NuGetGallery.Diagnostics
{
    public class GlimpseMessage : MessageBase, ITimelineMessage
    {
        public TimelineCategory EventCategory { get; set; }
        public string EventName { get; set; }
        public string EventSubText { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeSpan Offset { get; set; }
        public DateTime StartTime { get; set; }
    }
}
