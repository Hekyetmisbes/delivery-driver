using UnityEngine;
using System.Collections.Generic;

namespace DeliveryDriver.City
{
    /// <summary>
    /// Represents a neighborhood in the city grid system.
    /// </summary>
    [System.Serializable]
    public class Neighborhood
    {
        [SerializeField] private string neighborhoodName;
        [SerializeField] private List<Vector2Int> gridCells = new List<Vector2Int>();
        [SerializeField] private Color debugColor = Color.cyan;

        public string NeighborhoodName
        {
            get => neighborhoodName;
            set => neighborhoodName = value;
        }

        public List<Vector2Int> GridCells => gridCells;
        public Color DebugColor => debugColor;

        public Neighborhood(string name)
        {
            neighborhoodName = name;
            debugColor = new Color(Random.value, Random.value, Random.value, 0.3f);
        }

        public void AddGridCell(Vector2Int cell)
        {
            if (!gridCells.Contains(cell))
            {
                gridCells.Add(cell);
            }
        }

        public void AddGridCells(IEnumerable<Vector2Int> cells)
        {
            foreach (var cell in cells)
            {
                AddGridCell(cell);
            }
        }

        public bool ContainsGridCell(Vector2Int cell)
        {
            return gridCells.Contains(cell);
        }

        public Vector3 GetCenterPosition(float cellSize, Vector3 gridOrigin)
        {
            if (gridCells.Count == 0)
            {
                return gridOrigin;
            }

            Vector2 sum = Vector2.zero;
            foreach (var cell in gridCells)
            {
                sum += new Vector2(cell.x, cell.y);
            }

            Vector2 average = sum / gridCells.Count;
            return gridOrigin + new Vector3(average.x * cellSize, 0, average.y * cellSize);
        }

        public Bounds GetBounds(float cellSize, Vector3 gridOrigin)
        {
            if (gridCells.Count == 0)
            {
                return new Bounds(gridOrigin, Vector3.one);
            }

            Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue);
            Vector2Int max = new Vector2Int(int.MinValue, int.MinValue);

            foreach (var cell in gridCells)
            {
                min.x = Mathf.Min(min.x, cell.x);
                min.y = Mathf.Min(min.y, cell.y);
                max.x = Mathf.Max(max.x, cell.x);
                max.y = Mathf.Max(max.y, cell.y);
            }

            Vector3 minPos = gridOrigin + new Vector3(min.x * cellSize, 0, min.y * cellSize);
            Vector3 maxPos = gridOrigin + new Vector3((max.x + 1) * cellSize, 0, (max.y + 1) * cellSize);
            Vector3 center = (minPos + maxPos) * 0.5f;
            Vector3 size = maxPos - minPos;

            return new Bounds(center, size);
        }
    }
}
