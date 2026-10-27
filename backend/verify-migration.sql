-- Verification queries for the unified place model.
--
-- Run against the Tabiji database after:
--   dotnet ef database update
--
-- A query marked "(expected: 0 rows)" is a data-integrity check. A query
-- marked "(for the record)" is an inventory and may report pre-existing data.

SET NOCOUNT ON;

PRINT '--- 1. Place counts by kind (for the record) ---';
SELECT [Kind], COUNT(*) AS n
FROM [TripDestinations]
GROUP BY [Kind]
ORDER BY [Kind];

PRINT '--- 2. Legacy archive and migrated place count (expected: equal) ---';
SELECT (SELECT COUNT(*) FROM [SavedPlaces_Legacy])                         AS LegacyRows,
       (SELECT COUNT(*) FROM [TripDestinations]
         WHERE [MigratedFromSavedPlace] = 1)                               AS MigratedRows;

PRINT '--- 3. Every legacy saved place was copied with its data (expected: 0 rows) ---';
SELECT p.[Id], p.[Name], d.[Name] AS CopiedName
FROM [SavedPlaces_Legacy] p
LEFT JOIN [TripDestinations] d ON d.[Id] = p.[Id]
WHERE d.[Id] IS NULL
   OR d.[Name] <> p.[Name]
   OR d.[Latitude] <> p.[Latitude]
   OR d.[Longitude] <> p.[Longitude]
   OR ISNULL(d.[Category], '') <> ISNULL(p.[Category], '')
   OR ISNULL(d.[Address], '') <> ISNULL(p.[Address], '')
   OR ISNULL(d.[Notes], '') <> ISNULL(p.[Notes], '')
   OR ISNULL(d.[PlannedOn], '1900-01-01') <> ISNULL(p.[PlannedOn], '1900-01-01');

PRINT '--- 4. Kind and parent agree (expected: 0 rows) ---';
SELECT [Id], [Name], [Kind], [ParentDestinationId]
FROM [TripDestinations]
WHERE ([Kind] = 'Stop' AND [ParentDestinationId] IS NULL)
   OR ([Kind] IN ('Destination', 'Place') AND [ParentDestinationId] IS NOT NULL);

PRINT '--- 5. A stop''s parent is missing or in another trip (expected: 0 rows) ---';
SELECT s.[Id], s.[Name], s.[ParentDestinationId], p.[TripId] AS ParentTripId
FROM [TripDestinations] s
LEFT JOIN [TripDestinations] p ON p.[Id] = s.[ParentDestinationId]
WHERE s.[Kind] = 'Stop'
  AND (p.[Id] IS NULL OR p.[TripId] <> s.[TripId] OR p.[Kind] <> 'Destination');

PRINT '--- 6. A stop nested more than one level deep (expected: 0 rows) ---';
SELECT s.[Id], s.[Name], s.[ParentDestinationId]
FROM [TripDestinations] s
JOIN [TripDestinations] p ON p.[Id] = s.[ParentDestinationId]
WHERE s.[Kind] = 'Stop' AND p.[Kind] <> 'Destination';

PRINT '--- 7. Duplicate positions in any scope (expected: 0 rows) ---';
SELECT [TripId], [Kind], [ParentDestinationId], [SortOrder], COUNT(*) AS n
FROM [TripDestinations]
GROUP BY [TripId], [Kind], [ParentDestinationId], [SortOrder]
HAVING COUNT(*) > 1;

PRINT '--- 8. Stop stay dates outside their destination or trip (expected: 0 rows) ---';
SELECT t.[Name] AS Trip, p.[Name] AS Parent, s.[Name] AS Stop,
       s.[StartsOn], s.[EndsOn], p.[StartsOn] AS ParentStarts, p.[EndsOn] AS ParentEnds
FROM [TripDestinations] s
JOIN [Trips] t ON t.[Id] = s.[TripId]
JOIN [TripDestinations] p ON p.[Id] = s.[ParentDestinationId]
WHERE s.[Kind] = 'Stop'
  AND (
       (s.[StartsOn] IS NOT NULL AND (s.[StartsOn] < t.[StartsOn] OR s.[StartsOn] > t.[EndsOn]))
    OR (s.[EndsOn]   IS NOT NULL AND (s.[EndsOn]   < t.[StartsOn] OR s.[EndsOn]   > t.[EndsOn]))
    OR (p.[StartsOn] IS NOT NULL AND s.[StartsOn] IS NOT NULL AND s.[StartsOn] < p.[StartsOn])
    OR (p.[EndsOn]   IS NOT NULL AND s.[EndsOn]   IS NOT NULL AND s.[EndsOn]   > p.[EndsOn])
  );

PRINT '--- 9. Stops or places that end before they start (expected: 0 rows) ---';
SELECT [Id], [Name], [Kind], [StartsOn], [EndsOn]
FROM [TripDestinations]
WHERE [StartsOn] IS NOT NULL AND [EndsOn] IS NOT NULL AND [EndsOn] < [StartsOn];

PRINT '--- 10. Planned days outside the trip (expected: 0 rows) ---';
SELECT t.[Name] AS Trip, d.[Name] AS Place, d.[Kind], d.[PlannedOn]
FROM [TripDestinations] d
JOIN [Trips] t ON t.[Id] = d.[TripId]
WHERE d.[Kind] IN ('Stop', 'Place')
  AND d.[PlannedOn] IS NOT NULL
  AND (d.[PlannedOn] < t.[StartsOn] OR d.[PlannedOn] > t.[EndsOn]);

PRINT '--- 11. Half a pair of coordinates, or a position off the globe (expected: 0 rows) ---';
SELECT [Id], [Name], [Kind], [Latitude], [Longitude]
FROM [TripDestinations]
WHERE ([Latitude] IS NULL AND [Longitude] IS NOT NULL)
   OR ([Latitude] IS NOT NULL AND [Longitude] IS NULL)
   OR ([Latitude]  IS NOT NULL AND ([Latitude]  < -90  OR [Latitude]  > 90))
   OR ([Longitude] IS NOT NULL AND ([Longitude] < -180 OR [Longitude] > 180));

PRINT '--- 12. Main-route dates that contradict the route order (for the record) ---';
WITH [Ranked] AS (
    SELECT d.[TripId], d.[Name], d.[SortOrder], d.[StartsOn], d.[EndsOn],
           MAX(ISNULL(d.[EndsOn], d.[StartsOn])) OVER (
               PARTITION BY d.[TripId]
               ORDER BY d.[SortOrder]
               ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING) AS [Floor]
    FROM [TripDestinations] d
    WHERE d.[Kind] = 'Destination'
)
SELECT t.[Name] AS Trip, r.[Name] AS Destination, r.[StartsOn], r.[EndsOn], r.[Floor]
FROM [Ranked] r
JOIN [Trips] t ON t.[Id] = r.[TripId]
WHERE r.[Floor] IS NOT NULL AND r.[StartsOn] IS NOT NULL AND r.[StartsOn] < r.[Floor];

PRINT '--- 13. The archive table is present and the old table is gone (expected: 1, 0) ---';
SELECT CASE WHEN OBJECT_ID('SavedPlaces_Legacy', 'U') IS NULL THEN 0 ELSE 1 END AS LegacyTable,
       CASE WHEN OBJECT_ID('SavedPlaces', 'U') IS NULL THEN 0 ELSE 1 END       AS OldTable;

PRINT '--- 14. Structural inventory (for the record) ---';
SELECT (SELECT COUNT(*) FROM [Trips])                           AS Trips,
       (SELECT COUNT(*) FROM [TripDestinations]
         WHERE [Kind] = 'Destination')                           AS Destinations,
       (SELECT COUNT(*) FROM [TripDestinations]
         WHERE [Kind] = 'Stop')                                  AS Stops,
       (SELECT COUNT(*) FROM [TripDestinations]
         WHERE [Kind] = 'Place')                                 AS UnfiledPlaces,
       (SELECT COUNT(*) FROM [TripDestinations]
         WHERE [MigratedFromSavedPlace] = 1)                     AS Migrated,
       (SELECT COUNT(*) FROM [SavedPlaces_Legacy])               AS LegacyRows;

PRINT '--- 15. Every unfiled place has no parent (expected: 0 rows) ---';
SELECT [Id], [Name], [ParentDestinationId]
FROM [TripDestinations]
WHERE [Kind] = 'Place' AND [ParentDestinationId] IS NOT NULL;

PRINT '--- 16. Every destination is on the main route (expected: 0 rows) ---';
SELECT [Id], [Name], [ParentDestinationId]
FROM [TripDestinations]
WHERE [Kind] = 'Destination' AND [ParentDestinationId] IS NOT NULL;
