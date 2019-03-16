// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;

namespace NuGet.VerifyMicrosoftPackage
{
    public class Program
    {
        public static int Main(string[] args)
        {
            return Run(args, Console.Out);
        }

        public static int Run(string[] args, TextWriter console)
        {
            var app = new Application(console);

            try
            {
                return app.Execute(args);
            }
            catch (CommandParsingException ex)
            {
                app.OutputColor(
                    ConsoleColor.Red,
                    () =>
                    {
                        app.Out.WriteLine(ex.Message);
                    });
                app.ShowHelp();
                return -1;
            }
            catch (Exception ex)
            {
                app.OutputColor(
                    ConsoleColor.Red,
                    () =>
                    {
                        app.Out.WriteLine("An exception occurred.");
                        app.Out.WriteLine(ex);
                    });
                return -2;
            }
        }
    }
}
