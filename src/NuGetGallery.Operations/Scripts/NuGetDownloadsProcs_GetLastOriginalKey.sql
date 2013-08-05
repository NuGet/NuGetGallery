

IF OBJECT_ID('[dbo].[GetLastOriginalKey]') IS NOT NULL
    DROP PROCEDURE [dbo].[GetLastOriginalKey]
GO

CREATE PROCEDURE [dbo].[GetLastOriginalKey]
@OriginalKey INT OUTPUT
AS
BEGIN
    SELECT @OriginalKey = LastOriginalKey
    FROM ReplicationMarker
END
GO

