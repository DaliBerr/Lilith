using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kernel.MapGrid
{
    [Serializable]
    public sealed class CellEntry
    {
        [SerializeField] private int x;
        [SerializeField] private int y;
        [SerializeField] private GameObject cellObject;

        public CellEntry(int x, int y, GameObject cellObject)
        {
            this.x = x;
            this.y = y;
            this.cellObject = cellObject;
        }

        public int X => x;
        public int Y => y;
        public Vector2Int Position => new(x, y);
        public GameObject CellObject => cellObject;
    }

    [DisallowMultipleComponent]
    public sealed class MapGridAuthoring : MonoBehaviour
    {
        private sealed class CellSurfaceRuntimeCacheEntry
        {
            public CellSurfaceRuntimeCacheEntry(
                GameObject cellObject,
                CellData cellData,
                Collider managedCollider,
                CellData.CellSurfaceType surfaceType)
            {
                CellObject = cellObject;
                CellData = cellData;
                ManagedCollider = managedCollider;
                SurfaceType = surfaceType;
            }

            public GameObject CellObject { get; }
            public CellData CellData { get; }
            public Collider ManagedCollider { get; set; }
            public CellData.CellSurfaceType SurfaceType { get; set; }
        }

        public const string GeneratedContentObjectName = "GeneratedContent";
        public const string GroundTagName = "Ground";
        public const string WallTagName = "Wall";

        [Header("Grid")]
        [SerializeField] private int gridWidth = 1;
        [SerializeField] private int gridHeight = 1;
        [SerializeField] private Vector2 cellSize = Vector2.one;

        [Header("Chunk")]
        [SerializeField] private int chunkWidthInCells = 8;
        [SerializeField] private int chunkHeightInCells = 8;

        [Header("Prefabs")]
        [SerializeField] private GameObject defaultCellPrefab;

        [Header("Coordinate Binding")]
        [SerializeField] private MapGridCoordinateBinding coordinateBinding = new();

        [SerializeField] private List<CellEntry> cellEntries = new();

        private readonly Dictionary<Vector2Int, GameObject> cellLookup = new();
        private readonly Dictionary<Vector2Int, CellSurfaceRuntimeCacheEntry> cellSurfaceCache = new();
        private readonly HashSet<Vector2Int> dirtyCellSurfaceCoordinates = new();
        private readonly List<Vector2Int> dirtyCellSurfaceBuffer = new();
        private bool isCellSurfaceCacheInitialized;

        public int GridWidth
        {
            get => gridWidth;
            set => gridWidth = Mathf.Max(1, value);
        }

        public int GridHeight
        {
            get => gridHeight;
            set => gridHeight = Mathf.Max(1, value);
        }

        public Vector2 CellSize
        {
            get => cellSize;
            set => cellSize = value;
        }

        public int ChunkWidthInCells
        {
            get => chunkWidthInCells;
            set => chunkWidthInCells = Mathf.Max(1, value);
        }

        public int ChunkHeightInCells
        {
            get => chunkHeightInCells;
            set => chunkHeightInCells = Mathf.Max(1, value);
        }

        public GameObject DefaultCellPrefab
        {
            get => defaultCellPrefab;
            set => defaultCellPrefab = value;
        }

        public MapGridCoordinateBinding CoordinateBinding
        {
            get => coordinateBinding ??= new MapGridCoordinateBinding();
            set => coordinateBinding = value ?? new MapGridCoordinateBinding();
        }

        public IReadOnlyList<CellEntry> Cells => cellEntries;
        public int IndexedCellCount => cellEntries?.Count ?? 0;
        public int ExpectedCellCount => gridWidth * gridHeight;
        public bool IsCellSurfaceCacheInitialized => isCellSurfaceCacheInitialized;
        public int CellSurfaceCacheCount => cellSurfaceCache.Count;
        public int DirtyCellSurfaceCount => dirtyCellSurfaceCoordinates.Count;
        public float WorldPlaneY => transform.TransformPoint(Vector3.zero).y;

        private void OnEnable()
        {
            TryRebuildLookupFromEntries(out _);
            InvalidateCellSurfaceCache();
        }

        private void OnValidate()
        {
            gridWidth = Mathf.Max(1, gridWidth);
            gridHeight = Mathf.Max(1, gridHeight);
            chunkWidthInCells = Mathf.Max(1, chunkWidthInCells);
            chunkHeightInCells = Mathf.Max(1, chunkHeightInCells);
            coordinateBinding ??= new MapGridCoordinateBinding();
            cellEntries ??= new List<CellEntry>();
            InvalidateCellSurfaceCache();
        }

        public bool ContainsCell(int x, int y)
        {
            return cellLookup.ContainsKey(new Vector2Int(x, y));
        }

        public GameObject GetCell(int x, int y)
        {
            cellLookup.TryGetValue(new Vector2Int(x, y), out var cell);
            return cell;
        }

        public bool TryGetCell(Vector2Int position, out GameObject cell)
        {
            return cellLookup.TryGetValue(position, out cell) && cell != null;
        }

        public bool IsValidGridCoordinate(int x, int y)
        {
            return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
        }

        public Vector3 GetCellLocalPosition(int x, int y)
        {
            return new Vector3(x * cellSize.x, 0f, y * cellSize.y);
        }

        /// <summary>
        /// Converts a local-space point on the map plane into a grid coordinate.
        /// </summary>
        /// <param name="localPoint">Point in MapRoot local space.</param>
        /// <param name="coordinates">Resolved cell coordinate when the point is inside the grid.</param>
        /// <returns>True when the point maps to a valid cell coordinate.</returns>
        public bool TryGetCellCoordinateFromLocalPoint(Vector3 localPoint, out Vector2Int coordinates)
        {
            coordinates = default;

            if (cellSize.x <= 0f || cellSize.y <= 0f)
            {
                return false;
            }

            var x = Mathf.FloorToInt((localPoint.x / cellSize.x) + 0.5f);
            var y = Mathf.FloorToInt((localPoint.z / cellSize.y) + 0.5f);
            if (!IsValidGridCoordinate(x, y))
            {
                return false;
            }

            coordinates = new Vector2Int(x, y);
            return true;
        }

        /// <summary>
        /// Converts a world-space point on the map plane into a grid coordinate.
        /// </summary>
        /// <param name="worldPoint">Point in world space.</param>
        /// <param name="coordinates">Resolved cell coordinate when the point is inside the grid.</param>
        /// <returns>True when the point maps to a valid cell coordinate.</returns>
        public bool TryGetCellCoordinateFromWorldPoint(Vector3 worldPoint, out Vector2Int coordinates)
        {
            return TryGetCellCoordinateFromLocalPoint(transform.InverseTransformPoint(worldPoint), out coordinates);
        }

        /// <summary>
        /// Returns the world-space center position for a grid cell.
        /// </summary>
        /// <param name="x">Grid X coordinate.</param>
        /// <param name="y">Grid Y coordinate.</param>
        /// <returns>World-space cell center.</returns>
        public Vector3 GetCellWorldPosition(int x, int y)
        {
            return transform.TransformPoint(GetCellLocalPosition(x, y));
        }

        public Vector2 GetGridLocalSize()
        {
            if (TryGetGeneratedCellLocalBounds(out var minCellCenter, out var maxCellCenter))
            {
                return new Vector2(
                    (maxCellCenter.x - minCellCenter.x) + cellSize.x,
                    (maxCellCenter.y - minCellCenter.y) + cellSize.y);
            }

            return new Vector2(gridWidth * cellSize.x, gridHeight * cellSize.y);
        }

        public Vector3 GetGridLocalCenter()
        {
            if (TryGetGeneratedCellLocalBounds(out var minCellCenter, out var maxCellCenter))
            {
                return new Vector3(
                    (minCellCenter.x + maxCellCenter.x) * 0.5f,
                    0f,
                    (minCellCenter.y + maxCellCenter.y) * 0.5f);
            }

            return new Vector3(
                (Mathf.Max(0, gridWidth - 1) * cellSize.x) * 0.5f,
                0f,
                (Mathf.Max(0, gridHeight - 1) * cellSize.y) * 0.5f);
        }

        public Vector3 GetGridWorldCenter()
        {
            return transform.TransformPoint(GetGridLocalCenter());
        }

        public float GetGridWorldWidth()
        {
            return transform.TransformVector(new Vector3(GetGridLocalSize().x, 0f, 0f)).magnitude;
        }

        public float GetGridWorldHeight()
        {
            return transform.TransformVector(new Vector3(0f, 0f, GetGridLocalSize().y)).magnitude;
        }

        private bool TryGetGeneratedCellLocalBounds(out Vector2 minCellCenter, out Vector2 maxCellCenter)
        {
            minCellCenter = default;
            maxCellCenter = default;

            if (cellEntries == null || cellEntries.Count == 0)
            {
                return false;
            }

            var hasValue = false;
            var minX = 0f;
            var minY = 0f;
            var maxX = 0f;
            var maxY = 0f;

            for (var i = 0; i < cellEntries.Count; i++)
            {
                var entry = cellEntries[i];
                var cellObject = entry?.CellObject;
                if (cellObject == null)
                {
                    continue;
                }

                var localPosition = transform.InverseTransformPoint(cellObject.transform.position);
                if (!hasValue)
                {
                    minX = maxX = localPosition.x;
                minY = maxY = localPosition.z;
                    hasValue = true;
                    continue;
                }

                minX = Mathf.Min(minX, localPosition.x);
                minY = Mathf.Min(minY, localPosition.z);
                maxX = Mathf.Max(maxX, localPosition.x);
                maxY = Mathf.Max(maxY, localPosition.z);
            }

            if (!hasValue)
            {
                return false;
            }

            minCellCenter = new Vector2(minX, minY);
            maxCellCenter = new Vector2(maxX, maxY);
            return true;
        }

        public Vector2Int GetChunkCoordinate(int x, int y)
        {
            return new Vector2Int(x / chunkWidthInCells, y / chunkHeightInCells);
        }

        public int GetLocalRowInChunk(int y)
        {
            return y % chunkHeightInCells;
        }

        public int GetChunkCountX()
        {
            return Mathf.CeilToInt(gridWidth / (float)chunkWidthInCells);
        }

        public int GetChunkCountY()
        {
            return Mathf.CeilToInt(gridHeight / (float)chunkHeightInCells);
        }

        public Transform FindGeneratedContentRoot()
        {
            return transform.Find(GeneratedContentObjectName);
        }

        public bool HasGeneratedContent()
        {
            var generatedRoot = FindGeneratedContentRoot();
            return generatedRoot != null && generatedRoot.childCount > 0;
        }

        /// <summary>
        /// Builds the runtime cache used by incremental Ground and Wall surface refreshes.
        /// </summary>
        /// <param name="error">Validation or cache initialization error.</param>
        /// <returns>True when every indexed cell was cached successfully.</returns>
        public bool TryInitializeCellSurfaceCache(out string error)
        {
            error = null;

            if (!TryRebuildLookupFromEntries(out error))
            {
                InvalidateCellSurfaceCache();
                return false;
            }

            if (cellEntries == null || cellEntries.Count == 0)
            {
                InvalidateCellSurfaceCache();
                error = "The map index is empty. Rebuild Index or generate the grid before initializing the cell surface cache.";
                return false;
            }

            cellSurfaceCache.Clear();
            dirtyCellSurfaceCoordinates.Clear();
            dirtyCellSurfaceBuffer.Clear();

            for (var i = 0; i < cellEntries.Count; i++)
            {
                var entry = cellEntries[i];
                if (entry == null)
                {
                    InvalidateCellSurfaceCache();
                    error = $"Cell entry at index {i} is null.";
                    return false;
                }

                if (!TryBuildCellSurfaceRuntimeCache(entry.Position, entry.CellObject, out var cacheEntry, out error))
                {
                    InvalidateCellSurfaceCache();
                    return false;
                }

                cellSurfaceCache[entry.Position] = cacheEntry;
            }

            isCellSurfaceCacheInitialized = true;
            return true;
        }

        /// <summary>
        /// Marks one cell as dirty so the next incremental refresh re-evaluates its Ground or Wall state.
        /// </summary>
        /// <param name="coordinates">Grid coordinates of the target cell.</param>
        /// <param name="error">Validation or lookup error.</param>
        /// <returns>True when the target cell was successfully marked dirty.</returns>
        public bool TryMarkCellSurfaceDirty(Vector2Int coordinates, out string error)
        {
            error = null;

            if (!TryEnsureCellSurfaceCacheInitialized(out error))
            {
                return false;
            }

            if (!cellSurfaceCache.ContainsKey(coordinates))
            {
                error = $"No cached cell surface exists at ({coordinates.x}, {coordinates.y}).";
                return false;
            }

            dirtyCellSurfaceCoordinates.Add(coordinates);
            return true;
        }

        /// <summary>
        /// Marks one cell as dirty so the next incremental refresh re-evaluates its Ground or Wall state.
        /// </summary>
        /// <param name="x">Grid X coordinate of the target cell.</param>
        /// <param name="y">Grid Y coordinate of the target cell.</param>
        /// <param name="error">Validation or lookup error.</param>
        /// <returns>True when the target cell was successfully marked dirty.</returns>
        public bool TryMarkCellSurfaceDirty(int x, int y, out string error)
        {
            return TryMarkCellSurfaceDirty(new Vector2Int(x, y), out error);
        }

        /// <summary>
        /// Marks every cached cell as dirty so the next refresh reapplies Ground or Wall state to the full map.
        /// </summary>
        /// <param name="error">Validation or cache initialization error.</param>
        /// <returns>True when the dirty set now contains every indexed cell.</returns>
        public bool TryMarkAllCellSurfacesDirty(out string error)
        {
            error = null;

            if (!TryEnsureCellSurfaceCacheInitialized(out error))
            {
                return false;
            }

            dirtyCellSurfaceCoordinates.Clear();
            foreach (var coordinates in cellSurfaceCache.Keys)
            {
                dirtyCellSurfaceCoordinates.Add(coordinates);
            }

            return true;
        }

        /// <summary>
        /// Refreshes only the cells currently marked dirty, using cached references instead of rescanning the whole map.
        /// </summary>
        /// <param name="refreshedCellCount">Number of dirty cells that actually changed tag or collider state.</param>
        /// <param name="error">Validation or refresh error.</param>
        /// <returns>True when every dirty cell was processed successfully.</returns>
        public bool TryRefreshDirtyGroundWallState(out int refreshedCellCount, out string error)
        {
            refreshedCellCount = 0;
            error = null;

            if (!TryEnsureCellSurfaceCacheInitialized(out error))
            {
                return false;
            }

            if (dirtyCellSurfaceCoordinates.Count <= 0)
            {
                return true;
            }

            dirtyCellSurfaceBuffer.Clear();
            foreach (var coordinates in dirtyCellSurfaceCoordinates)
            {
                dirtyCellSurfaceBuffer.Add(coordinates);
            }

            dirtyCellSurfaceCoordinates.Clear();
            for (var i = 0; i < dirtyCellSurfaceBuffer.Count; i++)
            {
                var coordinates = dirtyCellSurfaceBuffer[i];
                if (!TryRefreshCachedCellSurfaceState(coordinates, out var changed, out error))
                {
                    dirtyCellSurfaceCoordinates.Add(coordinates);
                    for (var remaining = i + 1; remaining < dirtyCellSurfaceBuffer.Count; remaining++)
                    {
                        dirtyCellSurfaceCoordinates.Add(dirtyCellSurfaceBuffer[remaining]);
                    }

                    dirtyCellSurfaceBuffer.Clear();
                    return false;
                }

                if (changed)
                {
                    refreshedCellCount++;
                }
            }

            dirtyCellSurfaceBuffer.Clear();
            return true;
        }

        /// <summary>
        /// Refreshes the indexed map cells so empty text cells become Ground and non-empty text cells become Wall.
        /// </summary>
        /// <param name="error">Validation or processing error.</param>
        /// <returns>True when every indexed cell was refreshed successfully.</returns>
        public bool TryRefreshGroundWallState(out string error)
        {
            if (!TryMarkAllCellSurfacesDirty(out error))
            {
                return false;
            }

            return TryRefreshDirtyGroundWallState(out _, out error);
        }

        public void ReplaceCellEntries(IList<CellEntry> entries)
        {
            cellEntries = CloneEntries(entries);
            TryRebuildLookupFromEntries(out _);
            InvalidateCellSurfaceCache();
        }

        public void ClearCellEntries()
        {
            cellEntries ??= new List<CellEntry>();
            cellEntries.Clear();
            cellLookup.Clear();
            InvalidateCellSurfaceCache();
        }

        public bool TryRebuildLookupFromEntries(out string error)
        {
            error = null;
            cellLookup.Clear();
            cellEntries ??= new List<CellEntry>();

            for (var i = 0; i < cellEntries.Count; i++)
            {
                var entry = cellEntries[i];
                if (entry == null)
                {
                    error = $"Cell entry at index {i} is null.";
                    cellLookup.Clear();
                    InvalidateCellSurfaceCache();
                    return false;
                }

                if (entry.CellObject == null)
                {
                    error = $"Cell entry ({entry.X}, {entry.Y}) is missing its GameObject reference.";
                    cellLookup.Clear();
                    InvalidateCellSurfaceCache();
                    return false;
                }

                if (!IsValidGridCoordinate(entry.X, entry.Y))
                {
                    error = $"Cell entry ({entry.X}, {entry.Y}) is out of grid bounds.";
                    cellLookup.Clear();
                    InvalidateCellSurfaceCache();
                    return false;
                }

                if (cellLookup.ContainsKey(entry.Position))
                {
                    error = $"Duplicate cell entry detected at ({entry.X}, {entry.Y}).";
                    cellLookup.Clear();
                    InvalidateCellSurfaceCache();
                    return false;
                }

                cellLookup.Add(entry.Position, entry.CellObject);
            }

            return true;
        }

        private bool TryEnsureCellSurfaceCacheInitialized(out string error)
        {
            if (isCellSurfaceCacheInitialized)
            {
                error = null;
                return true;
            }

            return TryInitializeCellSurfaceCache(out error);
        }

        private bool TryBuildCellSurfaceRuntimeCache(
            Vector2Int coordinates,
            GameObject cellObject,
            out CellSurfaceRuntimeCacheEntry cacheEntry,
            out string error)
        {
            cacheEntry = null;
            error = null;

            if (cellObject == null)
            {
                error = $"No indexed cell exists at ({coordinates.x}, {coordinates.y}).";
                return false;
            }

            if (!TryResolveCellSurfaceTargets(cellObject, out var cellData, out var managedCollider, out error))
            {
                return false;
            }

            cacheEntry = new CellSurfaceRuntimeCacheEntry(cellObject, cellData, managedCollider, cellData.SurfaceType);
            return true;
        }

        private bool TryGetCachedCellSurface(
            Vector2Int coordinates,
            out CellSurfaceRuntimeCacheEntry cacheEntry,
            out string error)
        {
            cacheEntry = null;
            error = null;

            if (!TryGetCell(coordinates, out var cellObject) || cellObject == null)
            {
                error = $"No indexed cell exists at ({coordinates.x}, {coordinates.y}). Rebuild Index or Rebuild Grid before refreshing cached cell surfaces.";
                return false;
            }

            if (cellSurfaceCache.TryGetValue(coordinates, out cacheEntry) &&
                IsCellSurfaceRuntimeCacheEntryValid(cellObject, cacheEntry))
            {
                return true;
            }

            if (!TryBuildCellSurfaceRuntimeCache(coordinates, cellObject, out cacheEntry, out error))
            {
                return false;
            }

            cellSurfaceCache[coordinates] = cacheEntry;
            return true;
        }

        private bool TryRefreshCachedCellSurfaceState(Vector2Int coordinates, out bool changed, out string error)
        {
            changed = false;
            error = null;

            if (!TryGetCachedCellSurface(coordinates, out var cacheEntry, out error))
            {
                return false;
            }

            cacheEntry.ManagedCollider = cacheEntry.CellData.ManagedCollider;
            cacheEntry.SurfaceType = cacheEntry.CellData.SurfaceType;

            if (IsSurfaceStateCurrent(cacheEntry))
            {
                return true;
            }

            if (!cacheEntry.CellData.TryRefreshSurfacePresentation())
            {
                error = $"Failed to refresh the surface presentation on '{cacheEntry.CellObject.name}'.";
                return false;
            }

            cacheEntry.ManagedCollider = cacheEntry.CellData.ManagedCollider;
            cacheEntry.SurfaceType = cacheEntry.CellData.SurfaceType;
            changed = true;
            return true;
        }

        private static bool TryResolveCellSurfaceTargets(GameObject cellObject, out CellData cellData, out Collider managedCollider, out string error)
        {
            cellData = null;
            managedCollider = null;
            error = null;

            if (cellObject == null)
            {
                error = "Cell object is null.";
                return false;
            }

            if (!cellObject.TryGetComponent(out cellData) || cellData == null)
            {
                error = $"Cell '{cellObject.name}' does not contain a CellData component.";
                return false;
            }

            if (!cellData.TryCacheSurfaceBindings())
            {
                error = $"Cell '{cellObject.name}' does not have valid wall/ground surface bindings configured on its CellData component.";
                return false;
            }

            managedCollider = cellData.ManagedCollider;
            if (managedCollider == null)
            {
                error = $"Cell '{cellObject.name}' does not have a managed Collider configured for surface '{cellData.SurfaceType}'.";
                return false;
            }

            return true;
        }

        private static bool IsSurfaceStateCurrent(CellSurfaceRuntimeCacheEntry cacheEntry)
        {
            if (cacheEntry == null || cacheEntry.CellObject == null || cacheEntry.CellData == null)
            {
                return false;
            }

            return cacheEntry.ManagedCollider != null &&
                   cacheEntry.SurfaceType == cacheEntry.CellData.SurfaceType &&
                   cacheEntry.CellData.IsSurfacePresentationCurrent();
        }

        private static bool IsCellSurfaceRuntimeCacheEntryValid(GameObject cellObject, CellSurfaceRuntimeCacheEntry cacheEntry)
        {
            return cacheEntry != null &&
                   cacheEntry.CellObject == cellObject &&
                   cacheEntry.CellData != null &&
                   cacheEntry.CellData.gameObject == cellObject;
        }

        private void InvalidateCellSurfaceCache()
        {
            isCellSurfaceCacheInitialized = false;
            cellSurfaceCache.Clear();
            dirtyCellSurfaceCoordinates.Clear();
            dirtyCellSurfaceBuffer.Clear();
        }

        private static List<CellEntry> CloneEntries(IList<CellEntry> entries)
        {
            var clone = new List<CellEntry>(entries?.Count ?? 0);
            if (entries == null)
            {
                return clone;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                clone.Add(new CellEntry(entry.X, entry.Y, entry.CellObject));
            }

            clone.Sort(static (left, right) =>
            {
                var yCompare = left.Y.CompareTo(right.Y);
                return yCompare != 0 ? yCompare : left.X.CompareTo(right.X);
            });
            return clone;
        }
    }

}
