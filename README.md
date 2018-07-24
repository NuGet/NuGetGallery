NuGet.Jobs
==============

1. Each job would be an exe with 2 main classes Program and Job
2. Program.Main should simply do the following and nothing more

    ```
    var job = new Job();
    JobRunner.Run(job, args).Wait();
    ```
    
3. Job class must inherit `NuGet.Jobs.Common.JobBase` and implement abstract methods `Init` and `Run`
4. An `IDictionary<string, string>` is passed to `Init` for the job to initialize the member variables
5. Edit the project file on a new job and always set `<DefineConstants>TRACE</DefineConstants>` irrespective of configuration
6. Also, add a post-build event command line:

    ```
    move /y App.config <jobName\>.exe.config
    ```
    
7. Also, add settings.job file to mark the job as singleton, if the job will be run as a webjob, and it be a continuously running singleton

## Specific instructions

This repository has a lot of jobs used for completely different purposes. For this reason, each should should have its
own documentation. This specific documentation is far from complete... but you have to start somewhere!

- [Monitoring.RebootSearchInstance](src/Monitoring.RebootSearchInstance/README.md) - check each region for stuck search instances and restart them

Open Source Code of Conduct
===================
This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
