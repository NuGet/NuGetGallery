// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.EntityClient;
using System.Data.Objects;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet;
using NuGetGallery.Packaging;

namespace NuGetGallery.Operations.Tasks
{
    [Command("handlefailededits", "Handle Failed Package Edits", AltName = "hfe", MaxArgs = 0)]
    public class HandleFailedPackageEditsTask : DatabaseTask
    {

        [Option("Email account to be used to send the mail", AltName = "ua")]
        public string UserAccount { get; set; }

        [Option("Email password to be used to send the mail", AltName = "p")]
        public string Password { get; set; }

        [Option("Email host to be used to send the mail", AltName = "eh")]
        public string EmailHost { get; set; }
        public override void ExecuteCommand()
        {
            //Get all the failed edits.
            var connectionString = ConnectionString.ConnectionString;           
            var entitiesContext = new EntitiesContext(connectionString, readOnly: true);
            var failedEdits = entitiesContext.Set<PackageEdit>()
                .Where(pe => pe.TriedCount == 3).Include(pe => pe.Package).Include(pe => pe.Package.PackageRegistration);

         
            //For each ofthe failed edit, send out a support request mail.
            foreach (PackageEdit edit in failedEdits)
            { 
                Log.Info(
               "Sending support request for  '{0}'",
               edit.Package.PackageRegistration.Id);
                SendMailTask mailTask = new SendMailTask
                {
                    ConnectionString = this.ConnectionString,
                    UserAccount = this.UserAccount,
                    Password = this.Password,
                    EmailHost = this.EmailHost,
                    ToList = this.UserAccount,
                    ReplyToList = this.UserAccount,
                    MailSubject = string.Format(" [NuGet Gallery] : Package Edit Request for {0}", edit.Package.PackageRegistration.Id),
                    MailContent = string.Format("<b><i>Package:</i></b>  {0} </br> <b>Version:</b>  {1} </br> <b>TimeStamp:</b>  {2} </br> <b>LastError:</b>  {3} </br> <i>Message sent from NuGet Gallery</i> ", edit.Package.PackageRegistration.Id, edit.Package.NormalizedVersion,  edit.Timestamp,edit.LastError)
                };
                try
                {
                    mailTask.Execute();
                }
                catch (Exception e)
                {
                    Log.Error("Creating support request for package {0} failed with error {1}", edit.Package.PackageRegistration.Id, e.Message);
                }                
            }
        }
    }
}
