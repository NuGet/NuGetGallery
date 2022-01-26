# Gallery Tools

This project contains tools for performing manual one-time/maintenance operations on the Gallery.

---

## Backfill tools

There are several variants of the backfill tool, for different data properties. All of the tools have a similar pattern and set of capabilities. These tools collect repository metadata for packages in the database using .nuspec or .nupkg files in the V3 flat container and update the database with this data.

There are two ways to use the backfill tools:

- Collect metadata and the update metadata for all packages (two different executions of the tool, `-c` then `-u`)
- Collect and update metadata for specific packages (single execution of the tool, `-i`)

The commands that are backfill tools are:

- `fillrepodata` - updates repository URL metadata
  - The `-f` option defaults to `repositoryMetadata.txt`
- `filldevdeps` - sets the dev dependency bool
  - The `-f` option defaults to `developmentDependencyMetadata.txt`
- `filltfms` - updates framework compatibility
  - The `-f` option defaults to `tfmMetadata.txt`

All errors are written to the `errors.txt` file, placed in the current working directory.

---

### Collecting metadata

1. Configure app.config with database information
1. Run this tool with: `GalleryTools.exe {tool} -c -s {v3-url}`

`{tool}` is one of the backfill tool names above, e.g. `fillrepodata`.

`{v3-url}` is the V3 service index URL, e.g. `https://api.nuget.org/v3/index.json`.

This will create a file at the default data file location (or whatever is specified by the `-f | --file` option) with all collected data. You can stop the job anytime and restart. `cursor.txt` contains current position.    

---

### Updating the database with collected data

This is run after collecting metadata (the previous section).

1. Configure app.config with database information (should be already done for the `-c` invocation mentioned above)
1. Run `GalleryTools.exe {tool} -u`

`{tool}` is one of the backfill tool names above, e.g. `fillrepodata`.

This will update the database from the default data file (or whatever is specified by the `-f | --file` option). You can stop the job anytime and restart. `cursor.txt` contains current position. 

---

### Update specific packages

1. Configure app.config with database information
1. Create a data file with one package identity per line. The format is `{id},{version}`, e.g. `Newtonsoft.Json,9.0.1`.
1. Run `GalleryTools.exe {tool} -i -f {path-to-file} -s {v3-url}`

Instead of performing a time consuming collect and update flow for the entire data, you can perform the backfill on a specific set of package IDs and versions.

`{tool}` is one of the backfill tool names above, e.g. `fillrepodata`.

`{path-to-file}` is the path to the file with package identities, `{id},{version}` per line.

`{v3-url}` is the V3 service index URL, e.g. `https://api.nuget.org/v3/index.json`.

A file will be created at `{path-to-file}.completed` containing the list of all package identities that have been completed. This can be deleted to redo the update.
