# NuGet Gallery Operations Toolkit

This is a set of operations tools designed for the official NuGet.org site. It's sole purpose is to make it easier for the NuGet Gallery team to maintain the site. We believe it might be useful to those running their own instances of the NuGet Gallery application, so we have open-sourced it. We are very unlikely to take Pull Requests that don't directly enhance our workflows, but please do feel free to make and maintain forks for your own purposes and contribute back general-purpose changes that you think we might find helpful as well.

## What's inside
This repo contains NuGet Gallery Operations tools. These tools include:

1. A PowerShell Console Environment for working with deployed versions of the NuGet Gallery
2. An Azure Worker Role which performs database backups
3. An Operations application which can perform ops tasks such as deleting packages, adding them to curated feeds, managing backups, etc.
4. A set of Monitoring components used to monitor the status of the gallery and it's associated resources (SQL Databases, Blob storage, etc.)

## Getting Started
Getting started with this toolkit is easy. Make sure you have git installed (duh) and clone the repo. Then run "NuGetOps.cmd" to start the console. Create a shortcut to NuGetOps.cmd for easy access (there's even an icon included which you can use for the console).
