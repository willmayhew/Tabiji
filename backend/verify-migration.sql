-- The verification queries from the Phase 0.9 plan.
-- Run against the Tabiji database after `dotnet ef database update`.

SET NOCOUNT ON;

PRINT '--- 1. Mismatches between the legacy value and the first stop (expected: 0 rows) ---';
SELECT t.[Id] AS TripId, t.[Destination] AS LegacyValue, d.[Name] AS FirstStop
FROM [Trips] t
LEFT JOIN [TripDestinations] d
       ON d.[TripId] = t.[Id] AND d.[SortOrder] = 0
WHERE t.[Destination] IS NOT NULL
  AND (d.[Id] IS NULL OR LTRIM(RTRIM(t.[Destination])) <> d.[Name]);

PRINT '--- 2. Counts (expected: Stops = Trips - TripsWithoutStops) ---';
SELECT (SELECT COUNT(*) FROM [Trips])                        AS Trips,
       (SELECT COUNT(*) FROM [TripDestinations])            AS Stops,
       (SELECT COUNT(*) FROM [Trips] t
         WHERE NOT EXISTS (SELECT 1 FROM [TripDestinations] d
                            WHERE d.[TripId] = t.[Id]))     AS TripsWithoutStops;

PRINT '--- 3. Duplicate positions (expected: 0 rows) ---';
SELECT [TripId], [SortOrder], COUNT(*) AS n
FROM [TripDestinations] GROUP BY [TripId], [SortOrder] HAVING COUNT(*) > 1;

PRINT '--- 4. The backfilled routes, for the record ---';
SELECT t.[Name] AS Trip, t.[Destination] AS LegacyValue, d.[SortOrder], d.[Name] AS Stop
FROM [Trips] t
LEFT JOIN [TripDestinations] d ON d.[TripId] = t.[Id]
ORDER BY t.[StartsOn], t.[Name], d.[SortOrder];

PRINT '--- 5. Stops whose dates fall outside their trip (expected: 0 rows) ---';
SELECT t.[Name] AS Trip, d.[Name] AS Stop, d.[StartsOn], d.[EndsOn]
FROM [TripDestinations] d
JOIN [Trips] t ON t.[Id] = d.[TripId]
WHERE (d.[StartsOn] IS NOT NULL AND (d.[StartsOn] < t.[StartsOn] OR d.[StartsOn] > t.[EndsOn]))
   OR (d.[EndsOn]   IS NOT NULL AND (d.[EndsOn]   < t.[StartsOn] OR d.[EndsOn]   > t.[EndsOn]));

PRINT '--- 6. Stops that end before they start (expected: 0 rows) ---';
SELECT t.[Name] AS Trip, d.[Name] AS Stop, d.[StartsOn], d.[EndsOn]
FROM [TripDestinations] d
JOIN [Trips] t ON t.[Id] = d.[TripId]
WHERE d.[StartsOn] IS NOT NULL AND d.[EndsOn] IS NOT NULL AND d.[EndsOn] < d.[StartsOn];

PRINT '--- 7. Stops whose dates contradict the order of the route (expected: 0 rows) ---';
WITH [Ranked] AS (
    SELECT d.[TripId], d.[Name], d.[SortOrder], d.[StartsOn], d.[EndsOn],
           MAX(ISNULL(d.[EndsOn], d.[StartsOn])) OVER (
               PARTITION BY d.[TripId]
               ORDER BY d.[SortOrder]
               ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING) AS [Floor]
    FROM [TripDestinations] d
)
SELECT t.[Name] AS Trip, r.[Name] AS Stop, r.[StartsOn], r.[EndsOn], r.[Floor]
FROM [Ranked] r
JOIN [Trips] t ON t.[Id] = r.[TripId]
WHERE r.[Floor] IS NOT NULL AND r.[StartsOn] IS NOT NULL AND r.[StartsOn] < r.[Floor];

PRINT '--- 8. Stops that carry half a pair of coordinates (expected: 0 rows) ---';
SELECT t.[Name] AS Trip, d.[Name] AS Stop, d.[Latitude], d.[Longitude]
FROM [TripDestinations] d
JOIN [Trips] t ON t.[Id] = d.[TripId]
WHERE (d.[Latitude] IS NULL AND d.[Longitude] IS NOT NULL)
   OR (d.[Latitude] IS NOT NULL AND d.[Longitude] IS NULL);

PRINT '--- 9. Stops whose coordinates fall off the globe (expected: 0 rows) ---';
SELECT t.[Name] AS Trip, d.[Name] AS Stop, d.[Latitude], d.[Longitude]
FROM [TripDestinations] d
JOIN [Trips] t ON t.[Id] = d.[TripId]
WHERE (d.[Latitude]  IS NOT NULL AND (d.[Latitude]  < -90  OR d.[Latitude]  > 90))
   OR (d.[Longitude] IS NOT NULL AND (d.[Longitude] < -180 OR d.[Longitude] > 180));
