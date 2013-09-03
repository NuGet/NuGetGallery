/* DELETES Users with duplicated Usernames from the database. DOESNT DELETE the first User with a particular Username.
   
   Useful for: we used to not have a unique username cosntraint, and the user registration process can therefore 
   create duplicate users by a race condition. This scripts must be run before the migration which adds the UNIQUE
   constraint (index) on the User Username column.
   */

WITH NumberedRows
AS 
(    
   SELECT Row_number() OVER 
   (PARTITION BY Username ORDER BY Username, [Key] ASC)
   RowId, * from Users
)

DELETE FROM NumberedRows WHERE RowId > 1