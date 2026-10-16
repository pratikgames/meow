using System;
using UnityEngine;

namespace PuzzleGame
{
    /// <summary>Owns the board occupancy state and creates its colored-region cells.</summary>
    public sealed class GridManager : MonoBehaviour
    {
        public const int DefaultSize = 9;
        private static readonly Color[] DefaultRegionColors =
        {
            new Color(0.92f, 0.73f, 0.70f), new Color(0.93f, 0.82f, 0.62f), new Color(0.89f, 0.88f, 0.65f),
            new Color(0.69f, 0.83f, 0.70f), new Color(0.64f, 0.83f, 0.82f), new Color(0.66f, 0.78f, 0.91f),
            new Color(0.74f, 0.72f, 0.90f), new Color(0.86f, 0.71f, 0.87f), new Color(0.91f, 0.72f, 0.78f)
        };

        [SerializeField, Min(1)] private int rows = DefaultSize;
        [SerializeField, Min(1)] private int columns = DefaultSize;
        [SerializeField, Min(1)] private int regionSize = 3;
        [SerializeField, Min(0.1f)] private float cellSize = 0.31f;
        [SerializeField, Min(0f)] private float cellGap = 0.02f;
        [SerializeField, Min(0f)] private float regionGap = 0f;
        [SerializeField] private Vector2 bottomLeft = new Vector2(-1.525f, -1.525f);
        private int[] regionMap;

        private bool[,] occupied;
        private bool[] regionHasToken;
        private bool[] rowHasToken;
        private bool[] columnHasToken;
        private GridCell[,] cellViews;

        public event Action<Vector2Int> TokenPlaced;
        public int Rows => rows;
        public int Columns => columns;
        public int RegionCount { get; private set; }

        private void Awake()
        {
            rows = Mathf.Max(1, rows);
            columns = Mathf.Max(1, columns);
            regionSize = Mathf.Max(1, regionSize);
            InitializeDefaultRegions();
            InitializeState();
            BuildBoard();
            if (GetComponent<GameSession>() == null) gameObject.AddComponent<GameSession>();
        }

        private void InitializeDefaultRegions()
        {
            regionMap = new int[rows * columns];
            int regionColumns = Mathf.Max(1, columns / regionSize);
            RegionCount = Mathf.Max(1, (rows / regionSize) * regionColumns);
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++) regionMap[y * columns + x] = (y / regionSize) * regionColumns + x / regionSize;
        }

        private void InitializeState()
        {
            occupied = new bool[columns, rows];
            regionHasToken = new bool[RegionCount];
            rowHasToken = new bool[rows];
            columnHasToken = new bool[columns];
            cellViews = new GridCell[columns, rows];
        }

        /// <summary>Replace board dimensions and per-cell regions from an authored level.</summary>
        public void ConfigureBoard(int newRows, int newColumns, int[] authoredRegions)
        {
            newRows = Mathf.Max(1, newRows);
            newColumns = Mathf.Max(1, newColumns);
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            rows = newRows;
            columns = newColumns;
            bottomLeft = new Vector2(-(columns * (cellSize + cellGap) - cellGap) * 0.5f,
                -(rows * (cellSize + cellGap) - cellGap) * 0.5f);
            regionMap = new int[rows * columns];
            int maxRegion = -1;
            if (authoredRegions != null && authoredRegions.Length == regionMap.Length)
            {
                for (int i = 0; i < regionMap.Length; i++)
                {
                    regionMap[i] = Mathf.Max(0, authoredRegions[i]);
                    maxRegion = Mathf.Max(maxRegion, regionMap[i]);
                }
            }
            else
            {
                int regionColumns = Mathf.Max(1, columns / regionSize);
                for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    int id = (y / regionSize) * regionColumns + x / regionSize;
                    regionMap[y * columns + x] = id;
                    maxRegion = Mathf.Max(maxRegion, id);
                }
            }
            RegionCount = maxRegion + 1;
            InitializeState();
            BuildBoard();
        }

        private void BuildBoard()
        {
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                int region = GetRegionId(new Vector2Int(column, row));
                var cellObject = new GameObject($"Cell {row},{column}");
                cellObject.transform.SetParent(transform, false);
                cellObject.transform.position = CellCenter(column, row);
                cellObject.transform.localScale = Vector3.one * cellSize;

                var renderer = cellObject.AddComponent<SpriteRenderer>();
                renderer.sprite = PlaceholderVisuals.SquareSprite;
                renderer.sortingOrder = 0;
                PlaceholderVisuals.Configure(renderer);

                cellObject.AddComponent<BoxCollider2D>();

                var view = cellObject.AddComponent<GridCell>();
                view.Initialize(new Vector2Int(column, row), region, GetRegionColor(region), renderer);
                cellViews[column, row] = view;
            }
        }

        private Vector3 CellCenter(int column, int row)
        {
            float x = bottomLeft.x + cellSize * 0.5f + column * (cellSize + cellGap) + (column / regionSize) * regionGap;
            float y = bottomLeft.y + cellSize * 0.5f + row * (cellSize + cellGap) + (row / regionSize) * regionGap;
            return new Vector3(x, y, 0f);
        }

        public bool ContainsWorldPosition(Vector2 position)
        {
            Vector3 first = CellCenter(0, 0);
            Vector3 last = CellCenter(columns - 1, rows - 1);
            float halfCell = cellSize * 0.5f;
            return position.x >= first.x - halfCell && position.x <= last.x + halfCell
                && position.y >= first.y - halfCell && position.y <= last.y + halfCell;
        }

        public bool IsInside(Vector2Int cell) => cell.x >= 0 && cell.x < columns && cell.y >= 0 && cell.y < rows;
        public bool IsOccupied(Vector2Int cell) => !IsInside(cell) || occupied[cell.x, cell.y];
        public int GetRegionId(Vector2Int cell) => regionMap[cell.y * columns + cell.x];
        private Color GetRegionColor(int region) => DefaultRegionColors[region % DefaultRegionColors.Length];
        public bool RegionHasToken(int region) => regionHasToken[region];
        public bool RowHasToken(int row) => rowHasToken[row];
        public bool ColumnHasToken(int column) => columnHasToken[column];

        public bool HasAdjacentToken(Vector2Int cell)
        {
            for (int y = cell.y - 1; y <= cell.y + 1; y++)
            for (int x = cell.x - 1; x <= cell.x + 1; x++)
            {
                if (x == cell.x && y == cell.y) continue;
                if (IsInside(new Vector2Int(x, y)) && occupied[x, y]) return true;
            }
            return false;
        }

        public GridCell GetCellView(Vector2Int cell) => IsInside(cell) ? cellViews[cell.x, cell.y] : null;

        public bool TryPlaceToken(Vector2Int cell)
        {
            if (!IsInside(cell) || occupied[cell.x, cell.y]) return false;
            int region = GetRegionId(cell);
            occupied[cell.x, cell.y] = true;
            regionHasToken[region] = true;
            rowHasToken[cell.y] = true;
            columnHasToken[cell.x] = true;
            cellViews[cell.x, cell.y].SetOccupied();
            TokenPlaced?.Invoke(cell);
            return true;
        }

        public void ResetBoard()
        {
            occupied = new bool[columns, rows];
            regionHasToken = new bool[RegionCount];
            rowHasToken = new bool[rows];
            columnHasToken = new bool[columns];
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
                cellViews[x, y].ResetCell(GetRegionColor(GetRegionId(new Vector2Int(x, y))));
        }

        public bool TryPlaceGiven(Vector2Int cell)
        {
            if (!TryPlaceToken(cell)) return false;
            cellViews[cell.x, cell.y].SetGiven();
            return true;
        }
    }
}
