
CREATE PROCEDURE [dbo].[DequeueInvocation]
	@InstanceName nvarchar(100),
	@VisibilityDelayInMilliseconds int = 0
AS
	-- Atomic "Test and set" style dequeue that makes the message invisible again for the provided visibility delay 
	WITH cte
	AS (
		SELECT TOP (1) *
		FROM Invocations WITH (rowlock, readpast)
		WHERE [NextVisibleAt] <= SYSDATETIMEOFFSET() 
			AND Complete = 0
		ORDER BY [NextVisibleAt]
	)
	UPDATE	cte
	SET		[Status] = 'Executing',
			[Dequeued] = 1,
			[DequeueCount] = [DequeueCount] + 1,
			[LastInstanceName] = @InstanceName,
			[LastDequeuedAt] = SYSDATETIMEOFFSET(),
			[NextVisibleAt] = DATEADD(ms, @VisibilityDelayInMilliseconds, SYSDATETIMEOFFSET()),
			[LastUpdatedAt] = SYSDATETIMEOFFSET()
	OUTPUT inserted.*