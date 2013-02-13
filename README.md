# NuGet Operations Toolkit

This repo contains NuGet Gallery Operations tools. These tools include:
1. A PowerShell Console Environment for working with deployed versions of the NuGet Gallery
2. An Azure Worker Role which performs database backups
3. An Operations application which can perform ops tasks such as deleting packages, adding them to curated feeds, managing backups, etc.
4. A set of PowerShell scripts which wrap that application.

**Important:** This repository does not include ANY access keys or login credentials required to perform deployments, those are held internally. Contact someone on the NuGet team directly for more information.

## Getting Started
Getting started with this toolkit is easy. Make sure you have git installed (duh) and clone the repo. Then run "NuGetOps.cmd" to start the console. Create a shortcut to NuGetOps.cmd for easy access (there's even an icon included which you can use for the console).