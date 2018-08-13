# Gallery Tools

This project contains tools for performing manual one-time/maintenance operations on the Gallery.

## Backfill repository metadata

This tool collect repository metadata for all packages in the DB from nuspec files in V3 flat container and updates DB with this data.
Usage:
1. To collect repository metadata:
    a. Configure app.config with DB information and service index url
    b. Run this tool with: GalleryTools.exe fillrepodata -c
This will create a file repositoryMetadata.txt with all collected data. You can stop the job anytime and restart. cursor.txt contains current position.    
     
2. To update DB:
    a. Run GalleryTools.exe fillrepodata -u  
This will update DB from file repositoryMetadata.txt. You can stop the job anytime and restart.
    