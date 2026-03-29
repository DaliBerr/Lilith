using UnityEngine;

[DisallowMultipleComponent]
public sealed class CellData : MonoBehaviour
{
    [SerializeField] private int gridX;
    [SerializeField] private int gridY;

    public int GridX => gridX;
    public int GridY => gridY;
    public Vector2Int Coordinates => new(gridX, gridY);

    public void SetCoordinates(int x, int y)
    {
        gridX = x;
        gridY = y;
    }

    public void SetCoordinates(Vector2Int coordinates)
    {
        SetCoordinates(coordinates.x, coordinates.y);
    }

    public Vector2Int GetCoordinates()
    {
        return new Vector2Int(gridX, gridY);
    }
}
