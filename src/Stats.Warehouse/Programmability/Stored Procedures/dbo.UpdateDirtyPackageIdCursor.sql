CREATE PROCEDURE [dbo].[UpdateDirtyPackageIdCursor]
	@Position DATETIME NULL
AS
BEGIN

	IF NOT EXISTS (SELECT [Name] FROM [dbo].[Cursors] WHERE [Name] = 'GetDirtyPackageId')
		BEGIN
			INSERT INTO [dbo].[Cursors] ([Name], [Position])
			VALUES	('GetDirtyPackageId', @Position)
		END
	ELSE
		BEGIN
			UPDATE	[dbo].[Cursors]
			SET		[Position] = @Position
			WHERE	[Name] = 'GetDirtyPackageId'
		END
END