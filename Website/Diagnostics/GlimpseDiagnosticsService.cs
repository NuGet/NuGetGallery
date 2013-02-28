using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using Glimpse.Core.Extensibility;
using Glimpse.Core.Message;
using NuGet;

namespace NuGetGallery.Diagnostics
{
    public class GlimpseDiagnosticsService : IDiagnosticsService, IInspector
    {
        public static readonly TimelineCategory NuGetGallery = new TimelineCategory("NuGet Gallery", "#CCCC00", "#FFFF00");

        private IInspectorContext _context;

        public IDisposable Time(string title, string subTitle)
        {
            var timer = _context.TimerStrategy();
            var time = timer.Start();
            return new DisposableAction(() =>
            {
                var result = timer.Stop(time);
                _context.MessageBroker.Publish(
                    new GlimpseMessage()
                        .AsTimedMessage(result)
                        .AsTimelineMessage(NuGetGallery, eventSubText: subTitle));
            });
        }

        public void Setup(IInspectorContext context)
        {
            _context = context;
        }
    }
}