using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PuzzleGame
{
    /// <summary>Single-click or drag to toggle X marks; double-click/double-tap to place a cat.</summary>
    public sealed class BoardTapController : MonoBehaviour
    {
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private GridManager grid;
        [SerializeField] private PlacementValidator validator;
        [SerializeField] private GameSession session;
        [SerializeField, Min(0.1f)] private float doubleTapWindow = 0.45f;

        private GridCell selectedCell;
        private float lastTapTime;
        private bool dragMarkState = true;
        private bool dragStopped;
        private readonly HashSet<GridCell> dragVisitedCells = new HashSet<GridCell>();

        private void Awake()
        {
            if (gameplayCamera == null) gameplayCamera = Camera.main;
            if (session == null) session = FindFirstObjectByType<GameSession>();
        }

        private void Update()
        {
            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                if (touch.press.wasPressedThisFrame)
                {
                    BeginPointer(touch.position.ReadValue());
                    return;
                }
                if (touch.press.isPressed)
                {
                    ContinueDrag(touch.position.ReadValue());
                    return;
                }
                if (touch.press.wasReleasedThisFrame)
                {
                    EndPointer();
                    return;
                }
            }
            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    BeginPointer(Mouse.current.position.ReadValue());
                    return;
                }
                if (Mouse.current.leftButton.isPressed)
                {
                    ContinueDrag(Mouse.current.position.ReadValue());
                    return;
                }
                if (Mouse.current.leftButton.wasReleasedThisFrame) EndPointer();
            }
        }

        private void BeginPointer(Vector2 screenPosition)
        {
            dragVisitedCells.Clear();
            GridCell startCell = GetCellAt(screenPosition);
            dragStopped = startCell == null;
            if (startCell != null) dragVisitedCells.Add(startCell);
            HandleTap(screenPosition);
            dragMarkState = startCell == null || startCell.IsTapMarked;
        }

        private void ContinueDrag(Vector2 screenPosition)
        {
            if (dragStopped) return;
            GridCell cell = GetCellAt(screenPosition);
            if (cell == null)
            {
                if (gameplayCamera == null || grid == null)
                {
                    dragStopped = true;
                    return;
                }
                Ray ray = gameplayCamera.ScreenPointToRay(screenPosition);
                if (Mathf.Abs(ray.direction.z) < 0.0001f)
                {
                    dragStopped = true;
                    return;
                }
                float distanceToBoard = -ray.origin.z / ray.direction.z;
                Vector3 worldPoint = ray.GetPoint(distanceToBoard);
                if (!grid.ContainsWorldPosition(worldPoint)) dragStopped = true;
                return;
            }
            if (!dragVisitedCells.Add(cell) || cell.IsRevealed) return;
            cell.SetTapMark(dragMarkState);
        }

        private void EndPointer()
        {
            dragVisitedCells.Clear();
            dragStopped = false;
        }

        private void HandleTap(Vector2 screenPosition)
        {
            // GridManager creates GameSession dynamically in Awake, so it may not exist yet
            // when this controller's Awake runs. Resolve it again before handling input.
            if (session == null) session = FindFirstObjectByType<GameSession>();
            if (gameplayCamera == null) return;
            var cell = GetCellAt(screenPosition);
            if (cell == null) { ClearSelection(); return; }
            float now = Time.unscaledTime;
            if (cell == selectedCell && now - lastTapTime <= doubleTapWindow)
            {
                if (session != null)
                {
                    bool correct = session.TryPlace(cell.Coordinate);
                    if (!correct) cell.SetWrong();
                    ClearSelection();
                    lastTapTime = now;
                    return;
                }
                bool valid = validator.IsValid(cell.Coordinate, out _);
                cell.SetSelected(true, valid);
                if (valid && grid.TryPlaceToken(cell.Coordinate)) ClearSelection();
                lastTapTime = now;
                return;
            }

            ClearSelection();
            selectedCell = cell;
            selectedCell.SetSelected(true);
            selectedCell.ToggleTapMark();
            lastTapTime = now;
        }

        private GridCell GetCellAt(Vector2 screenPosition)
        {
            if (gameplayCamera == null) return null;
            var hit = Physics2D.GetRayIntersection(gameplayCamera.ScreenPointToRay(screenPosition));
            return hit.collider != null ? hit.collider.GetComponent<GridCell>() : null;
        }

        private void ClearSelection()
        {
            if (selectedCell != null) selectedCell.SetSelected(false);
            selectedCell = null;
        }
    }
}
