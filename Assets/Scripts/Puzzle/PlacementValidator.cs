using UnityEngine;

namespace PuzzleGame
{
    /// <summary>Applies region, row, column, and king-move adjacency rules.</summary>
    public sealed class PlacementValidator : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        public void Initialize(GridManager board) => grid = board;

        public bool IsValid(Vector2Int cell, out string reason)
        {
            if (!grid.IsInside(cell)) { reason = "Choose a tile on the board."; return false; }
            if (grid.IsOccupied(cell)) { reason = "That tile is already occupied."; return false; }
            if (grid.RegionHasToken(grid.GetRegionId(cell))) { reason = "That region already has a token."; return false; }
            if (grid.RowHasToken(cell.y)) { reason = "That row already has a token."; return false; }
            if (grid.ColumnHasToken(cell.x)) { reason = "That column already has a token."; return false; }
            if (grid.HasAdjacentToken(cell)) { reason = "Tokens cannot touch, including diagonally."; return false; }
            reason = string.Empty;
            return true;
        }
    }
}
