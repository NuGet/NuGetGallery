using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebBackgrounder;

namespace NuGetGallery.Diagnostics
{
    public class ProcessPerfEvents: Job
    {
        public string LogDirectory { get; private set; }
        public IEnumerable<string> Queues { get; private set; }

        public ProcessPerfEvents(TimeSpan interval, string logDirectory, IEnumerable<string> queues, TimeSpan timeout) 
            : base("FlushLogs", interval, timeout)
        {
            LogDirectory = logDirectory;
            Queues = queues;
        }

        public override Task Execute()
        {
            return new Task(() => Task.WaitAll(Queues.Select(q => ProcessQueue(q)).ToArray()));
        }

        private async Task ProcessQueue(string queue)
        {
            try
            {
                var events = MessageQueue.GetBatch<PerfEvent>(queue)
                    .ToList();

                // Group by hour
                var groups = events.GroupBy(e => new { e.TimestampUtc.Date, e.TimestampUtc.Hour });

                await Task.WhenAll(groups.Select(async group =>
                {
                    if (group.Any())
                    {
                        string dir = Path.Combine(LogDirectory, queue);
                        if (!Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }

                        // Determine the file name for the log
                        string fileName = Path.Combine(
                            dir,
                            String.Format("{0:yyyyMMdd}{1}.csv", group.Key.Date, group.Key.Hour));

                        // Append to the log
                        using (var strm = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.None))
                        using (var writer = new StreamWriter(strm))
                        {
                            foreach (var evt in group)
                            {
                                var fields = evt.Fields.OrderBy(p => p.Key);
                                if (strm.Length == 0)
                                {
                                    await writer.WriteLineAsync(
                                        "Source,Timestamp,Duration," + String.Join(",", fields.Select(f => f.Key)));
                                    await writer.FlushAsync();
                                }
                                await writer.WriteLineAsync(
                                    CsvEscape(evt.Source) + "," +
                                    CsvEscape(evt.TimestampUtc.ToString("O")) + "," + 
                                    CsvEscape(evt.Duration.TotalMilliseconds.ToString("0.00")) + "," + 
                                    String.Join(",", fields.Select(f => CsvEscape(f.Value == null ? String.Empty : f.Value.ToString()))));
                            }
                        }
                    }
                }));
            }
            catch (Exception)
            {
                throw;
            }
        }

        private static string CsvEscape(string p)
        {
            if (p.Contains(','))
            {
                if (p.Contains('\"'))
                {
                    p = p.Replace("\"", "\\\"");
                }
                p = "\"" + p + "\"";
            }
            return p;
        }
    }
}
