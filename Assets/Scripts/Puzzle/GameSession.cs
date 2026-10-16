using System;
using UnityEngine;

namespace PuzzleGame
{
    /// <summary>Campaign progression, daily play, puzzle generation, and round flow.</summary>
    public sealed class GameSession : MonoBehaviour
    {
        private const int MaxLevels = 100;
        private const int LevelsPerPage = 25;
        private const int LevelPageCount = MaxLevels / LevelsPerPage;
        private const string CurrentLevelKey = "Meowdoku.CurrentLevel";
        private const string UnlockedLevelKey = "Meowdoku.UnlockedLevel";
        private const string CompletedLevelPrefix = "Meowdoku.Completed.";

        private GridManager grid;
        private PlacementValidator validator;
        private LevelSO[] authoredLevels;
        private int[] answer;
        private int lives;
        private int placed;
        private int level;
        private int highestUnlocked;
        private int levelPage;
        private bool daily;
        private bool ended;
        private bool solved;
        private bool showingLevelSelect;
        private string message;
        private float messageUntil;
        private GUIStyle titleStyle, bodyStyle, buttonStyle, smallStyle, panelStyle;
        private GUIStyle resultTitleStyle, resultBodyStyle, resultStarsStyle;

        private void Awake()
        {
            grid = FindFirstObjectByType<GridManager>();
            validator = FindFirstObjectByType<PlacementValidator>();
            if (grid == null) { enabled = false; return; }
            authoredLevels = Resources.LoadAll<LevelSO>("Levels");
            if (validator == null) validator = gameObject.AddComponent<PlacementValidator>();
            validator.Initialize(grid);
            grid.TokenPlaced += OnTokenPlaced;

            int previousProgress = Mathf.Clamp(PlayerPrefs.GetInt("Meowdoku.Level", 1), 1, MaxLevels);
            highestUnlocked = Mathf.Clamp(Mathf.Max(PlayerPrefs.GetInt(UnlockedLevelKey, 1), previousProgress), 1, MaxLevels);
            level = Mathf.Clamp(PlayerPrefs.GetInt(CurrentLevelKey, previousProgress), 1, highestUnlocked);
            StartLevel(level);
        }

        private void OnDestroy()
        {
            if (grid != null) grid.TokenPlaced -= OnTokenPlaced;
        }

        public bool TryPlace(Vector2Int cell)
        {
            if (ended || showingLevelSelect) return false;
            if (grid.IsOccupied(cell)) { ShowMessage("A cat is already resting here."); return false; }
            if (!validator.IsValid(cell, out string reason))
            {
                LoseHeart(reason);
                return false;
            }
            if (cell.y >= answer.Length || cell.x != answer[cell.y])
            {
                LoseHeart("Not this cat's territory. Check its row, column and color.");
                return false;
            }
            return grid.TryPlaceToken(cell);
        }

        private void OnTokenPlaced(Vector2Int cell)
        {
            placed++;
            if (placed < grid.Rows) { ShowMessage("Purrfect choice!"); return; }

            ended = true;
            solved = true;
            if (daily)
            {
                message = "Purrfect! Today's puzzle is complete.";
                return;
            }

            PlayerPrefs.SetInt(CompletedLevelPrefix + level, 1);
            string starsKey = CompletedLevelPrefix + level + ".BestStars";
            PlayerPrefs.SetInt(starsKey, Mathf.Max(PlayerPrefs.GetInt(starsKey, 0), lives));
            if (level < MaxLevels && highestUnlocked < level + 1)
            {
                highestUnlocked = level + 1;
                PlayerPrefs.SetInt(UnlockedLevelKey, highestUnlocked);
            }
            PlayerPrefs.Save();
            message = level < MaxLevels
                ? "Level " + level + " complete! Level " + (level + 1) + " is unlocked."
                : "Amazing! You completed all 100 levels!";
        }

        private void LoseHeart(string reason)
        {
            lives--;
            if (lives <= 0)
            {
                lives = 0;
                ended = true;
                solved = false;
                message = "Out of hearts. Restart the level and try again.";
            }
            else ShowMessage(reason + "  A heart is gone.");
        }

        private void ShowMessage(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + 2.4f;
        }

        private void StartLevel(int selectedLevel)
        {
            level = Mathf.Clamp(selectedLevel, 1, highestUnlocked);
            daily = false;
            showingLevelSelect = false;
            PlayerPrefs.SetInt(CurrentLevelKey, level);
            PlayerPrefs.Save();
            int clueCount = Mathf.Clamp(5 - ((level - 1) / 5), 1, 5);
            LevelSO authored = FindAuthoredLevel(level);
            if (authored != null)
                grid.ConfigureBoard(authored.Rows, authored.Columns, authored.CellRegions);
            else
                grid.ConfigureBoard(GridManager.DefaultSize, GridManager.DefaultSize, null);
            int[] authoredAnswer = null;
            Vector2Int[] authoredClues = null;
            if (authored != null && !authored.TryGetPuzzle(grid, out authoredAnswer, out authoredClues, out string error))
            {
                Debug.LogError("Invalid LevelSO '" + authored.name + "': " + error, authored);
                authored = null;
            }
            StartRound(level * 7919 + 1013, clueCount, authoredAnswer, authoredClues);
            string name = authored != null && !string.IsNullOrWhiteSpace(authored.LevelName) ? " · " + authored.LevelName : string.Empty;
            string difficulty = authored != null ? authored.Difficulty.ToString().ToUpperInvariant() : DifficultyName(level);
            message = "Level " + level + name + " · " + difficulty + ". One cat per color, row and column; cats cannot touch.";
        }

        private LevelSO FindAuthoredLevel(int number)
        {
            if (authoredLevels == null) return null;
            for (int i = 0; i < authoredLevels.Length; i++)
                if (authoredLevels[i] != null && authoredLevels[i].LevelNumber == number) return authoredLevels[i];
            return null;
        }

        private void StartDaily()
        {
            daily = true;
            showingLevelSelect = false;
            int seed = DateTime.UtcNow.Year * 1000 + DateTime.UtcNow.DayOfYear;
            StartRound(seed, 3);
            message = "Today's puzzle is ready. Come back tomorrow for a new one.";
        }

        private void StartRound(int seed, int clueCount, int[] authoredAnswer = null, Vector2Int[] authoredClues = null)
        {
            if (grid == null) return;
            grid.ResetBoard();
            lives = 3;
            placed = 0;
            ended = false;
            solved = false;
            answer = authoredAnswer ?? GenerateSolution(seed, grid.Rows, grid.Columns);
            clueCount = Mathf.Clamp(clueCount, 1, Mathf.Min(grid.Rows, answer.Length));
            if (authoredClues != null && authoredClues.Length > 0)
            {
                for (int i = 0; i < authoredClues.Length; i++) grid.TryPlaceGiven(authoredClues[i]);
            }
            else
            {
                for (int row = 0; row < clueCount; row++) grid.TryPlaceGiven(new Vector2Int(answer[row], row));
            }
        }

        private int[] GenerateSolution(int seed, int rows, int columns)
        {
            var result = new int[rows];
            var usedColumns = new bool[columns];
            int regionHeight = Mathf.Max(1, rows / 3);
            int regionWidth = Mathf.Max(1, columns / 3);
            var usedRegions = new bool[3, 3];
            var random = new System.Random(seed);
            return FindSolutionRow(0, result, usedColumns, usedRegions, random, regionHeight, regionWidth)
                ? result : BuildFallbackSolution(rows);
        }

        private bool FindSolutionRow(int row, int[] result, bool[] usedColumns, bool[,] usedRegions,
            System.Random random, int regionHeight, int regionWidth)
        {
            if (row == result.Length) return true;
            int columnCount = usedColumns.Length;
            var candidates = new int[columnCount];
            for (int i = 0; i < columnCount; i++) candidates[i] = i;
            for (int i = columnCount - 1; i > 0; i--)
            {
                int swap = random.Next(i + 1);
                int temp = candidates[i]; candidates[i] = candidates[swap]; candidates[swap] = temp;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                int column = candidates[i];
                if (usedColumns[column]) continue;
                if (row > 0 && Mathf.Abs(column - result[row - 1]) <= 1) continue;
                int regionRow = Mathf.Min(2, row / regionHeight);
                int regionColumn = Mathf.Min(2, column / regionWidth);
                if (usedRegions[regionRow, regionColumn]) continue;

                result[row] = column;
                usedColumns[column] = true;
                usedRegions[regionRow, regionColumn] = true;
                if (FindSolutionRow(row + 1, result, usedColumns, usedRegions, random, regionHeight, regionWidth)) return true;
                usedColumns[column] = false;
                usedRegions[regionRow, regionColumn] = false;
            }
            return false;
        }

        private int[] BuildFallbackSolution(int rows)
        {
            int[] fallback = { 0, 3, 6, 1, 4, 7, 2, 5, 8 };
            var result = new int[rows];
            for (int i = 0; i < rows; i++) result[i] = fallback[i % fallback.Length];
            return result;
        }

        private static string DifficultyName(int selectedLevel)
        {
            if (selectedLevel <= 10) return "EASY";
            if (selectedLevel <= 25) return "MEDIUM";
            if (selectedLevel <= 50) return "HARD";
            if (selectedLevel <= 75) return "EXPERT";
            return "MASTER";
        }

        private void OnGUI()
        {
            EnsureStyles();
            float w = Screen.width;
            float scale = Mathf.Clamp(w / 900f, 0.72f, 1.25f);
            titleStyle.fontSize = Mathf.RoundToInt(27 * scale);
            bodyStyle.fontSize = Mathf.RoundToInt(18 * scale);
            smallStyle.fontSize = Mathf.RoundToInt(16 * scale);
            buttonStyle.fontSize = Mathf.RoundToInt(16 * scale);
            resultTitleStyle.fontSize = Mathf.RoundToInt(25 * scale);
            resultBodyStyle.fontSize = Mathf.RoundToInt(16 * scale);
            resultStarsStyle.fontSize = Mathf.RoundToInt(28 * scale);
            float pad = 20f * scale;
            float headerH = 105f * scale;
            GUI.color = new Color(0.10f, 0.11f, 0.15f, 0.94f);
            GUI.Box(new Rect(0, 0, w, headerH), GUIContent.none, panelStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(pad, 10 * scale, w - 2 * pad, 38 * scale), "MEOWDOKU", titleStyle);
            GUI.Label(new Rect(pad, 50 * scale, w - 170 * scale, 32 * scale), daily ? "DAILY PUZZLE" : "LEVEL " + level + "  ·  " + DifficultyName(level), bodyStyle);
            GUI.Label(new Rect(w - 142 * scale, 50 * scale, 128 * scale, 32 * scale), HeartsText(), bodyStyle);

            float footerH = 84f * scale;
            float footerY = Screen.height - footerH;
            GUI.color = new Color(0.10f, 0.11f, 0.15f, 0.96f);
            GUI.Box(new Rect(0, footerY, w, footerH), GUIContent.none, panelStyle);
            GUI.color = Color.white;
            float buttonY = footerY + 12 * scale;
            float buttonH = 48 * scale;
            float gap = 8 * scale;
            float buttonWidth = (w - 2 * pad - 2 * gap) / 3f;

            if (!ended || showingLevelSelect)
            {
            if (GUI.Button(new Rect(pad, buttonY, buttonWidth, buttonH), "LEVELS", buttonStyle))
            {
                daily = false;
                showingLevelSelect = true;
                levelPage = Mathf.Clamp((level - 1) / LevelsPerPage, 0, LevelPageCount - 1);
            }
            if (GUI.Button(new Rect(pad + buttonWidth + gap, buttonY, buttonWidth, buttonH), "DAILY", buttonStyle)) StartDaily();

            string actionLabel = showingLevelSelect ? "CLOSE" : ended && solved && !daily && level < MaxLevels ? "NEXT LEVEL" : "RESTART";
            if (GUI.Button(new Rect(pad + 2 * (buttonWidth + gap), buttonY, buttonWidth, buttonH), actionLabel, buttonStyle))
            {
                if (showingLevelSelect) showingLevelSelect = false;
                else if (ended && solved && !daily && level < MaxLevels) StartLevel(level + 1);
                else if (daily) StartDaily();
                else StartLevel(level);
            }
            }

            if (showingLevelSelect) DrawLevelSelect(w, scale, headerH, footerY);
            else if (ended) DrawResultOverlay(w, scale);
            else if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
                DrawMessage(w, scale, pad, headerH);
        }

        private void DrawLevelSelect(float screenWidth, float scale, float headerH, float footerY)
        {
            float panelWidth = Mathf.Min(screenWidth - 28 * scale, 600 * scale);
            float panelHeight = Mathf.Min(380 * scale, footerY - headerH - 24 * scale);
            float x = (screenWidth - panelWidth) * 0.5f;
            float y = headerH + (footerY - headerH - panelHeight) * 0.5f;
            GUI.color = new Color(0.12f, 0.13f, 0.18f, 0.98f);
            GUI.Box(new Rect(x, y, panelWidth, panelHeight), GUIContent.none, panelStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 12 * scale, y + 8 * scale, panelWidth - 24 * scale, 30 * scale),
                "CHOOSE A LEVEL  ·  " + highestUnlocked + "/" + MaxLevels + " UNLOCKED", bodyStyle);

            float navY = y + 42 * scale;
            float navWidth = 92 * scale;
            GUI.enabled = levelPage > 0;
            if (GUI.Button(new Rect(x + 12 * scale, navY, navWidth, 32 * scale), "‹ PREV", buttonStyle)) levelPage--;
            GUI.enabled = levelPage < LevelPageCount - 1;
            if (GUI.Button(new Rect(x + panelWidth - navWidth - 12 * scale, navY, navWidth, 32 * scale), "NEXT ›", buttonStyle)) levelPage++;
            GUI.enabled = true;
            GUI.Label(new Rect(x + navWidth + 14 * scale, navY, panelWidth - 2 * navWidth - 28 * scale, 32 * scale),
                "PAGE " + (levelPage + 1) + " / " + LevelPageCount, smallStyle);

            float gridTop = navY + 40 * scale;
            float sidePad = 15 * scale;
            float cellGap = 6 * scale;
            float cellWidth = (panelWidth - 2 * sidePad - 4 * cellGap) / 5f;
            float cellHeight = Mathf.Min(42 * scale, (panelHeight - (gridTop - y) - 12 * scale - 4 * cellGap) / 5f);
            int firstLevel = levelPage * LevelsPerPage + 1;
            for (int i = 0; i < LevelsPerPage; i++)
            {
                int selectedLevel = firstLevel + i;
                int column = i % 5;
                int row = i / 5;
                float cellX = x + sidePad + column * (cellWidth + cellGap);
                float cellY = gridTop + row * (cellHeight + cellGap);
                bool unlocked = selectedLevel <= highestUnlocked;
                bool complete = PlayerPrefs.GetInt(CompletedLevelPrefix + selectedLevel, 0) == 1;
                string label = complete ? selectedLevel + " ✓" : selectedLevel.ToString();
                GUI.enabled = unlocked;
                if (GUI.Button(new Rect(cellX, cellY, cellWidth, cellHeight), unlocked ? label : "—", buttonStyle)) StartLevel(selectedLevel);
            }
            GUI.enabled = true;
        }

        private void DrawMessage(float w, float scale, float pad, float headerH)
        {
            float boxW = Mathf.Min(w - 2 * pad, 660 * scale);
            float boxH = ended ? 104 * scale : 70 * scale;
            float y = headerH + 8 * scale;
            GUI.color = new Color(1f, 0.98f, 0.93f, 0.97f);
            GUI.Box(new Rect((w - boxW) * 0.5f, y, boxW, boxH), GUIContent.none, panelStyle);
            GUI.color = new Color(0.20f, 0.16f, 0.22f);
            GUI.Label(new Rect((w - boxW) * 0.5f + 10 * scale, y + 5 * scale, boxW - 20 * scale, boxH - 10 * scale), message, smallStyle);
            GUI.color = Color.white;
        }

        private void DrawResultOverlay(float screenWidth, float scale)
        {
            GUI.color = new Color(0.06f, 0.07f, 0.10f, 0.82f);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, panelStyle);

            float panelWidth = Mathf.Min(screenWidth - 32 * scale, 430 * scale);
            float panelHeight = Mathf.Min(300 * scale, Screen.height - 36 * scale);
            float x = (screenWidth - panelWidth) * 0.5f;
            float y = (Screen.height - panelHeight) * 0.5f;
            GUI.color = new Color(1f, 0.97f, 0.89f, 1f);
            GUI.Box(new Rect(x, y, panelWidth, panelHeight), GUIContent.none, panelStyle);

            bool won = solved;
            GUI.color = won ? new Color(0.21f, 0.48f, 0.35f) : new Color(0.72f, 0.27f, 0.29f);
            string heading = daily
                ? (won ? "DAILY COMPLETE!" : "DAILY FAILED")
                : (won ? "LEVEL " + level + " COMPLETE!" : "LEVEL " + level + " FAILED");
            float innerX = x + 18 * scale;
            float innerWidth = panelWidth - 36 * scale;
            GUI.Label(new Rect(innerX, y + 16 * scale, innerWidth, 42 * scale), heading, resultTitleStyle);

            if (won && !daily)
            {
                int bestStars = PlayerPrefs.GetInt(CompletedLevelPrefix + level + ".BestStars", lives);
                string starRow = (lives >= 1 ? "★" : "☆") + "  " + (lives >= 2 ? "★" : "☆") + "  " + (lives >= 3 ? "★" : "☆");
                GUI.color = new Color(0.93f, 0.64f, 0.15f);
                GUI.Label(new Rect(innerX, y + 61 * scale, innerWidth, 34 * scale), starRow, resultStarsStyle);
                GUI.color = new Color(0.24f, 0.22f, 0.25f);
                GUI.Label(new Rect(innerX, y + 98 * scale, innerWidth, 42 * scale),
                    "Best: " + bestStars + " star" + (bestStars == 1 ? "" : "s") + (level < MaxLevels ? "\nLevel " + (level + 1) + " is unlocked." : "\nAll 100 levels complete!"), resultBodyStyle);
            }
            else
            {
                GUI.color = new Color(0.24f, 0.22f, 0.25f);
                string detail = won
                    ? "Today's puzzle is complete. Come back tomorrow for another."
                    : "All three hearts are gone. Try the puzzle again.";
                GUI.Label(new Rect(innerX, y + 70 * scale, innerWidth, 52 * scale), detail, resultBodyStyle);
            }

            string primaryLabel = won
                ? (daily ? "PLAY DAILY AGAIN" : level < MaxLevels ? "NEXT LEVEL" : "PLAY AGAIN")
                : (daily ? "TRY DAILY AGAIN" : "TRY AGAIN");
            float buttonHeight = 44 * scale;
            float buttonWidth = innerWidth;
            float primaryY = y + panelHeight - 102 * scale;
            GUI.color = Color.white;
            if (GUI.Button(new Rect(innerX, primaryY, buttonWidth, buttonHeight), primaryLabel, buttonStyle))
            {
                if (!won) { if (daily) StartDaily(); else StartLevel(level); }
                else if (daily) StartDaily();
                else if (level < MaxLevels) StartLevel(level + 1);
                else StartLevel(level);
            }
            if (GUI.Button(new Rect(innerX, primaryY + 51 * scale, buttonWidth, buttonHeight), "LEVEL MAP", buttonStyle))
            {
                showingLevelSelect = true;
                levelPage = Mathf.Clamp((level - 1) / LevelsPerPage, 0, LevelPageCount - 1);
            }
            GUI.color = Color.white;
        }

        private string HeartsText() => (lives > 0 ? "♥ " : "♡ ") + (lives > 1 ? "♥ " : "♡ ") + (lives > 2 ? "♥" : "♡");

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 27, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            titleStyle.normal.textColor = new Color(1f, 0.91f, 0.75f);
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            bodyStyle.normal.textColor = Color.white;
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            smallStyle.normal.textColor = new Color(0.20f, 0.16f, 0.22f);
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            buttonStyle.normal.textColor = new Color(0.20f, 0.16f, 0.22f);
            panelStyle = new GUIStyle(GUI.skin.box);
            resultTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            resultBodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            resultStarsStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }
    }
}
