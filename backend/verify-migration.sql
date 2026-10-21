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
