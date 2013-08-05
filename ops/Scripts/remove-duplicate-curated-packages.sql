/* DELETE duplicate CuratedPackages from the database, keeping just one of each (unique id = feed + package keys).
   FAVOR Keeping the entries that were manually added, or were added with notes 
   
   USEFUL FOR: Doing the CuratedFeed unique index migration that will prevent further duplication (we were seeing 40% of packages were dupes)
*/
 
WITH NumberedRows
AS 
(
    SELECT Row_number() OVER 
        (PARTITION BY CuratedFeedKey, PackageRegistrationKey ORDER BY CuratedFeedKey, PackageRegistrationKey, AutomaticallyCurated, Notes DESC)
        RowId, * from CuratedPackages
)
DELETE FROM NumberedRows WHERE RowId > 1
