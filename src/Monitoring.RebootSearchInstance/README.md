# Monitoring.RebootSearchInstance

There is a tricky bug in the search service today that sporadically causes a search index to stop loading in an
instance. This results stale search results in that region for some requests (whenever the load balancer happens
to hit that bad instance). This also results in a phone call to our on-call person due to index lag. No fun!

Therefore, this job runs in each environment (DEV, INT, PROD) as singleton and restarts stuck search instances.

## Configuration

The configuration files are JSON. There is one for DEV, INT, and PROD with placeholder tokens that are meant to be
replaced by Octopus.

## Running Locally

This section is written with the assumption that the reader is a NuGet team member.

**First,** open `Settings\dev.json` in a text editor and replace the `#{PROPERTY.NAME}` values with the correct values.
I recommend getting the values for our DEV environment from Octopus. I have put a filled out `dev.json` in OneNote so
you can get it from there. Note that it has some KeyVault placeholders in it so you need the DEV KeyVault certificate
installed on your machine-wide ("Local Machine") certificate store.

**Second,** build this project in Visual Studio 2017.

**Third,** open a command prompt in the `bin\Debug` directory and run the following command:


```
NuGet.Monitoring.RebootSearchInstance.exe -Configuration ..\..\Settings\dev.json
```

You can add the `-InstrumentationKey AI_KEY` option if you want the logs to go to ApplicationInsights.

This job will keep running for a day, checking each configured reason every 5 minutes.