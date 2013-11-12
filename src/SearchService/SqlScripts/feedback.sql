
USE Feedback
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
