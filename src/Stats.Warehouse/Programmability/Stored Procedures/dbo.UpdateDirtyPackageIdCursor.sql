CREATE PROCEDURE [dbo].[UpdateDirtyPackageIdCursor]
	@Position DATETIME NULL
AS
BEGIN
	UPDATE	[dbo].[Cursors]
	SET		[Position] = @Position
	WHERE	[Name] = 'GetDirtyPackageId'
END
