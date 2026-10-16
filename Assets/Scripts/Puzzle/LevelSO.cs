using UnityEngine;

namespace PuzzleGame
{
    public enum LevelDifficulty
    {
        Easy,
        Medium,
        Hard,
        Expert,
        Master
    }

    /// <summary>Authored puzzle data. Meow positions are the correct cat cells, indexed by row.</summary>
    [CreateAssetMenu(fileName = "Level_001", menuName = "Meowdoku/Level")]
    public sealed class LevelSO : ScriptableObject
    {
        [SerializeField, Min(1)] private int levelNumber = 1;
        [SerializeField] private string levelName = "First Pawsteps";
        [SerializeField] private LevelDifficulty difficulty = LevelDifficulty.Easy;
        [SerializeField, Min(1)] private int rows = GridManager.DefaultSize;
        [SerializeField, Min(1)] private int columns = GridManager.DefaultSize;
        [SerializeField, Tooltip("Region ID for each cell, in row-major order.")] private int[] cellRegions;
        [SerializeField, Tooltip("The correct Meow cell for each row. X is column; Y is row.")]
        private Vector2Int[] meowPositions;
        [SerializeField, Tooltip("Fixed Meows revealed at the start of this level.")]
        private Vector2Int[] startingMeowPositions;

        public int LevelNumber => levelNumber;
        public string LevelName => levelName;
        public LevelDifficulty Difficulty => difficulty;
        public int Rows => rows;
        public int Columns => columns;
        public int[] CellRegions => cellRegions;
        public Vector2Int[] MeowPositions => meowPositions;
        public Vector2Int[] StartingMeowPositions => startingMeowPositions;

        public bool TryGetPuzzle(GridManager grid, out int[] answerColumns, out Vector2Int[] startingMeows, out string error)
        {
            answerColumns = null;
            startingMeows = null;
            error = string.Empty;
            if (grid == null || grid.Rows != rows || grid.Columns != columns || meowPositions == null || meowPositions.Length != rows)
            {
                error = "The board dimensions and answer positions do not match.";
                return false;
            }
            if (cellRegions != null)
            {
                if (cellRegions.Length != rows * columns)
                {
                    error = "The colored-region layout must include every board cell.";
                    return false;
                }
                var regionsPresent = new bool[9];
                for (int i = 0; i < cellRegions.Length; i++)
                {
                    if (cellRegions[i] < 0 || cellRegions[i] >= regionsPresent.Length)
                    {
                        error = "Region IDs must use one of the nine available colors.";
                        return false;
                    }
                    regionsPresent[cellRegions[i]] = true;
                }
                int distinctRegions = 0;
                for (int i = 0; i < regionsPresent.Length; i++) if (regionsPresent[i]) distinctRegions++;
                if (distinctRegions != rows)
                {
                    error = "The board must use exactly one colored region per row.";
                    return false;
                }
            }

            answerColumns = new int[grid.Rows];
            var usedColumns = new bool[grid.Columns];
            var usedRegions = new bool[grid.RegionCount];
            for (int i = 0; i < meowPositions.Length; i++)
            {
                Vector2Int cell = meowPositions[i];
                if (!grid.IsInside(cell) || cell.y != i || usedColumns[cell.x])
                {
                    error = "Meow positions must be inside the board, one per row, and one per column.";
                    return false;
                }
                int region = grid.GetRegionId(cell);
                bool adjacent = i > 0 && Mathf.Abs(meowPositions[i - 1].x - cell.x) <= 1;
                if (usedRegions[region] || adjacent)
                {
                    error = "Meow positions must use each colored region once and cannot touch.";
                    return false;
                }
                answerColumns[i] = cell.x;
                usedColumns[cell.x] = true;
                usedRegions[region] = true;
            }

            startingMeows = startingMeowPositions;
            if (startingMeows == null || startingMeows.Length == 0) return true;
            for (int i = 0; i < startingMeows.Length; i++)
            {
                Vector2Int clue = startingMeows[i];
                if (!grid.IsInside(clue) || clue.y >= answerColumns.Length || answerColumns[clue.y] != clue.x)
                {
                    error = "Every starting Meow must match a correct Meow position.";
                    return false;
                }
                for (int j = 0; j < i; j++)
                    if (startingMeows[j] == clue)
                    {
                        error = "Starting Meow positions cannot be repeated.";
                        return false;
                    }
            }
            return true;
        }
    }
}
