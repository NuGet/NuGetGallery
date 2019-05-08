# Gallery Tools

This project contains tools for performing manual one-time/maintenance operations on the Gallery.

---

## Backfill repository metadata

This tool collects repository metadata for all packages in the database using nuspec files in the V3 flat container and updates the database with this data.

### Collecting metadata

1. Configure app.config with database information and service index url
1. Run this tool with: `GalleryTools.exe fillrepodata -c`

This will create a file `repositoryMetadata.txt` with all collected data. You can stop the job anytime and restart. `cursor.txt` contains current position.    

### Updating the database with collected data

1. Run `GalleryTools.exe fillrepodata -u`

This will update the database from file `repositoryMetadata.txt`. You can stop the job anytime and restart.  `cursor.txt` contains current position. 

---

## Backfill development dependency metadata

This tool collects development dependency metadata for all packages in the database using nuspec files in the V3 flat container and updates the database with this data.

### Collecting metadata

1. Configure app.config with database information and service index url
1. Run this tool with: `GalleryTools.exe filldevdeps -c`

This will create a file `developmentDependencyMetadata.txt` with all collected data. You can stop the job anytime and restart. `cursor.txt` contains current position.    

### Updating the database with collected data

1. Run `GalleryTools.exe filldevdeps -u`

This will update the database from file `developmentDependencyMetadata.txt`. You can stop the job anytime and restart.  `cursor.txt` contains current position.
