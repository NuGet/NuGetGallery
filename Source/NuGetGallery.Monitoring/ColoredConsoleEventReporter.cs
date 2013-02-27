using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring
{
    public class ColoredConsoleEventReporter : IEventReporter
    {
        private object _consoleLock = new object();

        public void Report(MonitoringEvent evt)
        {
            lock (_consoleLock)
            {
                switch (evt.Type)
                {
                    case EventType.Success:
                        WriteColored("pass:", ConsoleColor.Green);
                        HandleMessageEvent((MonitoringMessageEvent)evt);
                        break;
                    case EventType.Failure:
                        WriteColored("fail:", ConsoleColor.Red);
                        HandleMessageEvent((MonitoringMessageEvent)evt);
                        break;
                    case EventType.Degraded:
                        WriteColored("warn:", ConsoleColor.Yellow);
                        HandleMessageEvent((MonitoringMessageEvent)evt);
                        break;
                    case EventType.QualityOfService:
                        WriteColored("qos :", ConsoleColor.White);
                        HandleQoSEvent(evt);
                        break;
                    case EventType.MonitorFailure:
                        WriteColored("err :", ConsoleColor.Magenta);
                        HandleMessageEvent((MonitoringMessageEvent)evt);
                        break;
                    case EventType.Unhealthy:
                        WriteColored("weak:", ConsoleColor.Cyan);
                        HandleMessageEvent((MonitoringMessageEvent)evt);
                        break;
                    default:
                        WriteColored("unk :", ConsoleColor.Gray);
                        HandleEvent(evt);
                        break;
                }
            }
        }

        private void WriteColored(string message, ConsoleColor color)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(message);
            Console.ForegroundColor = oldColor;
        }

        private void HandleQoSEvent(MonitoringEvent evt)
        {
            HandleEvent(evt, () =>
            {
                var qosMessage = FormatQoS((MonitoringQoSEvent)evt);
                if (!String.IsNullOrEmpty(qosMessage))
                {
                    Console.Write(" ");
                    Console.Write(qosMessage);
                }
            });
        }

        private void HandleMessageEvent(MonitoringMessageEvent messageEvent)
        {
            HandleEvent(messageEvent);
        }

        private void HandleEvent(MonitoringEvent evt) { HandleEvent(evt, () => { }); }
        private void HandleEvent(MonitoringEvent evt, Action additionalWriters)
        {
            Console.Write(" [{0}] {1}", evt.Resource, evt.Action);
            additionalWriters();
            Console.WriteLine();
        }

        private string FormatQoS(MonitoringQoSEvent evt)
        {
            if (evt.Value is TimeSpan)
            {
                return String.Format("Time Taken: {0}", FormatTime((TimeSpan)evt.Value));
            }
            else if (evt.Value is int)
            {
                return String.Format("Value: {0}", (int)evt.Value);
            }
            return String.Empty;
        }

        private object FormatTime(TimeSpan timeSpan)
        {
            if (timeSpan.TotalSeconds < 1.0)
            {
                return timeSpan.Milliseconds.ToString() + "ms";
            }
            else if (timeSpan.TotalMinutes < 1.0)
            {
                return Math.Round(timeSpan.TotalSeconds, 2).ToString() + "s";
            }
            else if (timeSpan.TotalHours < 1.0)
            {
                return Math.Round(timeSpan.TotalMinutes, 2).ToString() + "min";
            }
            else if (timeSpan.TotalDays < 1.0)
            {
                return Math.Round(timeSpan.TotalHours, 2).ToString() + "hrs";
            }
            return Math.Round(timeSpan.TotalDays, 2).ToString() + "d";
        }
    }
}
