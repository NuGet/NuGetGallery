
USE Feedback
GO

DROP TABLE Feedback
GO

CREATE TABLE Feedback
(
	query varchar(300),
	prerelease varchar(30),
	sortBy varchar(30),
	expectedPackageId varchar(200),
	contactDetails varchar(200)
)
GO

CREATE CLUSTERED INDEX feedback_pk ON Feedback 
( 
 query,
 prerelease,
 sortBy,
 expectedPackageId,
 contactDetails
)
GO