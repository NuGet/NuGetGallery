using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.Hosting;
using Microsoft.Owin.StaticFiles;
using Microsoft.Owin.StaticFiles.ContentTypes;
using Owin;
using PowerArgs;

namespace CatMan
{
    public class ServeArgs
    {
        [ArgShortcut("l")]
        [ArgDescription("The path to serve at the root. Defaults to the current directory")]
        public string LocalPath { get; set; }

        [ArgShortcut("p")]
        [DefaultValue(3333)]
        [ArgDescription("The port on which to serve the files. Defaults to 3333")]
        public int Port { get; set; }
    }
    public partial class Commands
    {
        [ArgActionMethod]
        public void Serve(ServeArgs args)
        {
            if (String.IsNullOrEmpty(args.LocalPath))
            {
                args.LocalPath = Environment.CurrentDirectory;
            }

            Console.WriteLine("Starting server for {1} at http://localhost:{0}", args.Port, args.LocalPath);
            var startOptions = new StartOptions()
            {
                Port = args.Port
            };
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            Console.CancelKeyPress += (sender, a) =>
            {
                a.Cancel = true;
                tcs.SetResult(0);
            };
            using (WebApp.Start(startOptions, CreateOwinApp(args.LocalPath)))
            {
                Console.WriteLine("Server started. Press Ctrl-C to stop.");
                tcs.Task.Wait();
                Console.WriteLine("Stopping Server");
            }
        }

        private Action<IAppBuilder> CreateOwinApp(string localPath)
        {
            return app =>
            {
                app.Use(async (ctx, next) =>
                {
                    Console.WriteLine("{0} {1}", ctx.Request.Method, ctx.Request.Uri.ToString());
                    await next();
                    Console.WriteLine("{0} {1}", ctx.Response.StatusCode, ctx.Request.Uri.ToString());
                });

                var provider = new FileExtensionContentTypeProvider();
                provider.Mappings.Add(".json", "application/json");

                app.UseStaticFiles(new StaticFileOptions()
                {
                    FileSystem = new PhysicalFileSystem(localPath),
                    ContentTypeProvider = provider,
                    ServeUnknownFileTypes = true
                });
            };
        }
    }
}
