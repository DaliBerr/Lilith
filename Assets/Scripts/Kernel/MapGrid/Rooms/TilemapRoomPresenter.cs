using UnityEngine;
using UnityEngine.Tilemaps;
using Vocalith.Logging;

namespace Kernel.MapGrid
{
    [DisallowMultipleComponent]
    public sealed class TilemapRoomPresenter : MonoBehaviour
    {
        [SerializeField] private Tilemap floorTilemap;
        [SerializeField] private Tilemap wallTilemap;
        [SerializeField] private TileBase floorTile;
        [SerializeField] private TileBase wallTile;

        public Tilemap FloorTilemap => floorTilemap;
        public Tilemap WallTilemap => wallTilemap;
        public int LastAppliedFloorTileCount { get; private set; }
        public int LastAppliedWallTileCount { get; private set; }

        public bool TryApply(RoomResolvedLayout layout, out string error)
        {
            error = null;
            LastAppliedFloorTileCount = 0;
            LastAppliedWallTileCount = 0;

            if (layout == null)
            {
                error = "Room layout is missing.";
                return false;
            }

            if (floorTilemap == null || wallTilemap == null)
            {
                error = "TilemapRoomPresenter requires both floor and wall Tilemaps.";
                return false;
            }

            if (floorTile == null || wallTile == null)
            {
                error = "TilemapRoomPresenter requires both floor and wall Tiles.";
                return false;
            }

            Clear();
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    var tilePosition = new Vector3Int(x, y, 0);
                    if (layout.GetSurface(x, y) == CellData.CellSurfaceType.Wall)
                    {
                        wallTilemap.SetTile(tilePosition, wallTile);
                        LastAppliedWallTileCount++;
                        continue;
                    }

                    floorTilemap.SetTile(tilePosition, floorTile);
                    LastAppliedFloorTileCount++;
                }
            }

            floorTilemap.CompressBounds();
            wallTilemap.CompressBounds();
            return true;
        }

        public void Apply(RoomResolvedLayout layout)
        {
            if (!TryApply(layout, out string error))
            {
                GameDebug.LogError($"[TilemapRoomPresenter] {error}");
            }
        }

        public void Clear()
        {
            floorTilemap?.ClearAllTiles();
            wallTilemap?.ClearAllTiles();
        }
    }
}
