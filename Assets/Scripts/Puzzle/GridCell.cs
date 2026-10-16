using UnityEngine;

namespace PuzzleGame
{
    /// <summary>Visual and hit target for one tile in the logic board.</summary>
    public sealed class GridCell : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer tile;
        [SerializeField] private SpriteRenderer token;
        private Color regionColor;

        public Vector2Int Coordinate { get; private set; }
        public int RegionId { get; private set; }
        public bool IsOccupied { get; private set; }
        public bool IsGiven { get; private set; }
        public bool IsRevealed => IsOccupied || isWrong;
        public bool IsTapMarked => isTapMarked;
        private bool isWrong;
        private bool isTapMarked;

        public void Initialize(Vector2Int coordinate, int regionId, Color color, SpriteRenderer tileRenderer)
        {
            Coordinate = coordinate;
            RegionId = regionId;
            regionColor = color;
            tile = tileRenderer;
            tile.color = color;

            var marker = new GameObject("Token");
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            marker.transform.localScale = Vector3.one * 0.62f;
            token = marker.AddComponent<SpriteRenderer>();
            token.sprite = PlaceholderVisuals.CatSprite;
            token.color = new Color(0.98f, 0.96f, 0.90f);
            token.sortingOrder = 2;
            PlaceholderVisuals.Configure(token);
            token.enabled = false;
        }

        public void SetSelected(bool selected, bool valid = true)
        {
            if (IsOccupied || isWrong) return;
            tile.color = selected
                ? (valid ? Color.Lerp(regionColor, Color.white, 0.50f) : new Color(0.96f, 0.35f, 0.35f))
                : regionColor;
        }

        public void SetOccupied()
        {
            IsOccupied = true;
            var hitTarget = GetComponent<Collider2D>();
            if (hitTarget != null) hitTarget.enabled = false;
            tile.color = Color.Lerp(regionColor, new Color(0.50f, 0.52f, 0.57f), 0.42f);
            token.sprite = PlaceholderVisuals.CatSprite;
            token.color = new Color(0.98f, 0.96f, 0.90f);
            token.enabled = true;
        }

        public void ToggleTapMark()
        {
            SetTapMark(!isTapMarked);
        }

        public void SetTapMark(bool marked)
        {
            if (IsRevealed || isTapMarked == marked) return;
            isTapMarked = marked;
            if (isTapMarked)
            {
                token.sprite = PlaceholderVisuals.WrongSprite;
                token.color = Color.white;
                token.enabled = true;
            }
            else
            {
                token.sprite = PlaceholderVisuals.CatSprite;
                token.color = new Color(0.98f, 0.96f, 0.90f);
                token.enabled = false;
            }
        }

        public void SetWrong()
        {
            if (IsOccupied) return;
            isWrong = true;
            isTapMarked = false;
            var hitTarget = GetComponent<Collider2D>();
            if (hitTarget != null) hitTarget.enabled = false;
            tile.color = new Color(0.96f, 0.35f, 0.35f);
            token.sprite = PlaceholderVisuals.WrongSprite;
            token.color = new Color(0.72f, 0.05f, 0.08f);
            token.enabled = true;
        }

        public void SetGiven()
        {
            IsGiven = true;
            SetOccupied();
            token.color = new Color(1f, 0.94f, 0.73f);
            tile.color = Color.Lerp(regionColor, new Color(0.96f, 0.77f, 0.37f), 0.34f);
        }

        public void ResetCell(Color color)
        {
            regionColor = color;
            tile.color = color;
            token.enabled = false;
            token.sprite = PlaceholderVisuals.CatSprite;
            token.color = new Color(0.98f, 0.96f, 0.90f);
            IsOccupied = false;
            IsGiven = false;
            isWrong = false;
            isTapMarked = false;
            var hitTarget = GetComponent<Collider2D>();
            if (hitTarget != null) hitTarget.enabled = true;
        }
    }
}
