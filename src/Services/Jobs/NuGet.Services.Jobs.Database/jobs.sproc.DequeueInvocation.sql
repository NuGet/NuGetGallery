CREATE PROCEDURE [jobs].[DequeueInvocation]
(
	@InstanceName nvarchar(100),
	@HideUntil datetimeoffset
)
AS
	-- Find an available row to dequeue and insert a new one indicating it has been dequeued
	WITH cte
	AS (
		SELECT TOP (1) *
		FROM [jobs].ActiveInvocations WITH (rowlock, readpast)
		WHERE [NextVisibleAt] <= SYSDATETIMEOFFSET() 
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
			[UpdatedBy],
			[IsContinuation],
			[DequeueCount],
			[Complete],
			[Dequeued],
			[QueuedAt], 
			[NextVisibleAt],
			[UpdatedAt])
	OUTPUT	inserted.*
	SELECT	Id, 
			Job, 
			Source, 
			Payload, 
			'Dequeued' AS [Status],
			Result,
			@InstanceName AS [UpdatedBy],
			IsContinuation,
			DequeueCount + 1 AS [DequeueCount],
			Complete,
			1 AS [Dequeued],
			QueuedAt,
			@HideUntil AS [NextVisibleAt],
			SYSDATETIMEOFFSET() AS [UpdatedAt]
	FROM cte