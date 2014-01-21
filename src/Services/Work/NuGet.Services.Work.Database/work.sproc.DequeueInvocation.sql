CREATE PROCEDURE [work].[DequeueInvocation]
(
	@InstanceName nvarchar(100),
	@HideUntil datetime2
)
AS
	-- Find an available row to dequeue and insert a new one indicating it has been dequeued
	WITH cte
	AS (
		SELECT TOP (1) *
		FROM [work].ActiveInvocations WITH (rowlock, readpast)
		WHERE [NextVisibleAt] <= SYSUTCDATETIME() 
			AND Complete = 0
		ORDER BY [NextVisibleAt]
	)
	INSERT INTO [private].InvocationsStore(
            [Id],
            [Job],
            [Source],
            [Payload],
            [Status],
            [Result],
            [ResultMessage],
            [UpdatedBy],
            [LogUrl],
            [DequeueCount],
            [IsContinuation],
            [Complete],
            [LastDequeuedAt],
            [LastSuspendedAt],
            [CompletedAt],
            [QueuedAt],
            [NextVisibleAt],
            [UpdatedAt],
            [JobInstanceName])
	OUTPUT	inserted.*
	SELECT	Id, 
			Job, 
			Source, 
			Payload, 
			2 AS [Status], -- Dequeued
			Result,
            ResultMessage,
			@InstanceName AS [UpdatedBy],
            LogUrl,
			DequeueCount + 1 AS [DequeueCount],
			IsContinuation,
			Complete,
            SYSUTCDATETIME() AS [LastDequeuedAt],
            [LastSuspendedAt],
            [CompletedAt],
			QueuedAt,
			@HideUntil AS [NextVisibleAt],
			SYSUTCDATETIME() AS [UpdatedAt],
            [JobInstanceName]
	FROM cte