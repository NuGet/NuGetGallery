using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Canton
{
    public static class CantonUtilities
    {
        public static byte[] Compress(string data)
        {
            using (var compressedStream = new MemoryStream())
            {
                using (var zipStream = new GZipStream(compressedStream, CompressionMode.Compress))
                {
                    zipStream.Write(Encoding.UTF8.GetBytes(data), 0, data.Length);
                    zipStream.Close();
                    return compressedStream.ToArray();
                }
            }
        }

        public static string Decompress(byte[] data)
        {
            using (var compressedStream = new MemoryStream(data))
            {
                using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
                {
                    using (StreamReader reader = new StreamReader(zipStream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        public static void ReplaceIRI(IGraph graph, Uri oldIRI, Uri newIRI)
        {
            // replace the local IRI with the NuGet IRI
            string localUri = oldIRI.AbsoluteUri;

            var triples = graph.Triples.ToArray();

            string mainIRI = newIRI.AbsoluteUri;

            foreach (var triple in triples)
            {
                IUriNode subject = triple.Subject as IUriNode;
                IUriNode objNode = triple.Object as IUriNode;
                INode newSubject = triple.Subject;
                INode newObject = triple.Object;

                bool replace = false;

                if (subject != null && subject.Uri.AbsoluteUri.StartsWith(localUri))
                {
                    // TODO: store these mappings in a dictionary
                    Uri iri = new Uri(String.Format(CultureInfo.InvariantCulture, "{0}{1}", mainIRI, subject.Uri.AbsoluteUri.Substring(localUri.Length)));
                    newSubject = graph.CreateUriNode(iri);
                    replace = true;
                }

                if (objNode != null && objNode.Uri.AbsoluteUri.StartsWith(localUri))
                {
                    // TODO: store these mappings in a dictionary
                    Uri iri = new Uri(String.Format(CultureInfo.InvariantCulture, "{0}{1}", mainIRI, objNode.Uri.AbsoluteUri.Substring(localUri.Length)));
                    newObject = graph.CreateUriNode(iri);
                    replace = true;
                }

                if (replace)
                {
                    graph.Assert(newSubject, triple.Predicate, newObject);
                    graph.Retract(triple);
                }
            }
        }

        public static void Init()
        {
            System.Net.ServicePointManager.DefaultConnectionLimit = 1024;
        }

        public static void RunJobs(Queue<CantonJob> jobs)
        {
            CantonJob currentJob = null;
            bool run = true;

            ConsoleCancelEventHandler handler = (sender, e) =>
                {
                    if (run)
                    {
                        Console.WriteLine("Ctrl+C caught in Run");
                        e.Cancel = true;
                        run = false;

                        if (currentJob != null)
                        {
                            currentJob.Stop().Wait();
                        }
                    }
                    else
                    {
                        Console.WriteLine("2nd Ctrl+C caught in run, ignoring");
                    }
                };

            Console.CancelKeyPress += handler;

            try
            {
                foreach (var job in jobs)
                {
                    if (run)
                    {
                        currentJob = job;
                        currentJob.Run();
                        currentJob = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Log(ex.ToString(), "canton-job-exceptions.txt");
            }
            finally
            {
                Console.CancelKeyPress -= handler;
            }
        }

        /// <summary>
        /// Run a job multiple times in parallel
        /// </summary>
        public static void RunManyJobs(Queue<Func<CantonJob>> jobs, int instances)
        {
            ConcurrentBag<CantonJob> currentJobs = new ConcurrentBag<CantonJob>();
            bool run = true;

            ConsoleCancelEventHandler handler = (sender, e) =>
            {
                if (run)
                {
                    Console.WriteLine("Ctrl+C caught in Run");
                    e.Cancel = true;
                    run = false;

                    foreach (var job in currentJobs.ToArray())
                    {
                        job.Stop().Wait();
                    }
                }
                else
                {
                    Console.WriteLine("2nd Ctrl+C caught in Run ignoring");
                }
            };

            try
            {
                foreach (var getJob in jobs)
                {
                    if (run)
                    {
                        Stack<Task> tasks = new Stack<Task>(instances);
                        currentJobs = new ConcurrentBag<CantonJob>();

                        for (int i = 0; i < instances; i++)
                        {
                            tasks.Push(Task.Run(() =>
                                {
                                    CantonJob job = getJob();
                                    currentJobs.Add(job);
                                    job.Run();
                                }));
                        }

                        Task.WaitAll(tasks.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                Log(ex.ToString(), "canton-job-exceptions.txt");
            }
            finally
            {
                Console.CancelKeyPress -= handler;
            }
        }

        private static readonly object _lockObj = new object();
        public static void Log(string message, string file)
        {

            lock (_lockObj)
            {
                using (var writer = new StreamWriter(file, true))
                {
                    writer.WriteLine(String.Format(CultureInfo.InvariantCulture, "[{0}] {1}", DateTime.UtcNow.ToString("O"), message));
                }

                Console.WriteLine(message);
            }
        }
    }
}
