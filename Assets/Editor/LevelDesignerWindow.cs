using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PuzzleGame.Editor
{
    /// <summary>A compact click-to-author editor for Meowdoku LevelSO assets.</summary>
    public sealed class LevelDesignerWindow : EditorWindow
    {
        private const int DefaultSize = GridManager.DefaultSize;
        private static readonly Color[] RegionColors =
        {
            new Color(.92f, .73f, .70f), new Color(.93f, .82f, .62f), new Color(.89f, .88f, .65f),
            new Color(.69f, .83f, .70f), new Color(.64f, .83f, .82f), new Color(.66f, .78f, .91f),
            new Color(.74f, .72f, .90f), new Color(.86f, .71f, .87f), new Color(.91f, .72f, .78f)
        };

        private LevelSO level;
        private int levelNumber = 1;
        private string levelName = "New Level";
        private LevelDifficulty difficulty;
        private int rows = DefaultSize, columns = DefaultSize;
        private bool[,] answer = new bool[DefaultSize, DefaultSize];
        private bool[,] clues = new bool[DefaultSize, DefaultSize];
        private int[,] regions = new int[DefaultSize, DefaultSize];
        private int selectedRegion;
        private bool clueMode;
        private bool regionPaintMode;
        private bool paintingDrag;
        private Vector2 scroll;
        private string validationMessage;
        private MessageType validationType;

        [MenuItem("Meowdoku/Level Designer")]
        public static void Open() => GetWindow<LevelDesignerWindow>("Level Designer");

        private void OnEnable() => LoadLevel(level);

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Click one cat in every row. Use every column and colored region once. Cats cannot touch, even diagonally. Switch to Clues to choose cats shown at the start.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            var selected = (LevelSO)EditorGUILayout.ObjectField("Level Asset", level, typeof(LevelSO), false);
            if (EditorGUI.EndChangeCheck()) LoadLevel(selected);

            using (new EditorGUILayout.HorizontalScope())
            {
                levelNumber = EditorGUILayout.IntField("Number", levelNumber);
                levelName = EditorGUILayout.TextField("Name", levelName);
            }
            difficulty = (LevelDifficulty)EditorGUILayout.EnumPopup("Difficulty", difficulty);
            int newRows = EditorGUILayout.IntSlider("Rows / cats", rows, 1, 9);
            int newColumns = EditorGUILayout.IntSlider("Columns", columns, 1, 15);
            if (newRows != rows || newColumns != columns) ResizeBoard(newRows, newColumns);
            using (new EditorGUILayout.HorizontalScope())
            {
                clueMode = GUILayout.Toggle(clueMode, "Clue Mode", "Button", GUILayout.Width(90));
                regionPaintMode = GUILayout.Toggle(regionPaintMode, "Paint Regions", "Button", GUILayout.Width(105));
                GUILayout.Label(clueMode ? "Click an answer cat to toggle its starting clue." : regionPaintMode ? "Click cells to paint the selected region." : "Click to add or remove answer cats.");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Region to paint", GUILayout.Width(92));
                for (int i = 0; i < RegionColors.Length; i++)
                {
                    int regionId = i;
                    GUI.backgroundColor = RegionColors[i];
                    if (GUILayout.Button(selectedRegion == i ? "✓" : " ", GUILayout.Width(30), GUILayout.Height(24))) selectedRegion = regionId;
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.LabelField("Selected: Color " + (selectedRegion + 1), EditorStyles.miniLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawBoard();
            EditorGUILayout.EndScrollView();
            ValidateAnswer();
            EditorGUILayout.HelpBox(validationMessage, validationType);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear Cats")) ClearBoard();
                GUI.enabled = validationType != MessageType.Error && Count(answer) == rows;
                if (GUILayout.Button(level == null ? "Create Level Asset" : "Save Level")) SaveLevel();
                GUI.enabled = true;
            }
        }

        private void DrawBoard()
        {
            float cell = Mathf.Clamp((position.width - 44f) / columns, 24f, 58f);
            GUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8);
                for (int row = rows - 1; row >= 0; row--)
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        for (int col = 0; col < columns; col++)
                        {
                            int region = regions[col, row];
                            var old = GUI.backgroundColor;
                            GUI.backgroundColor = RegionColors[Mathf.Clamp(region, 0, RegionColors.Length - 1)];
                            string label = answer[col, row] ? (clues[col, row] ? "🐱★" : "🐱") : " ";
                            Rect cellRect = GUILayoutUtility.GetRect(cell, cell, GUILayout.Width(cell), GUILayout.Height(cell));
                            Event evt = Event.current;
                            if (regionPaintMode && evt.type == EventType.MouseDown && evt.button == 0 && cellRect.Contains(evt.mousePosition))
                            {
                                paintingDrag = true;
                                regions[col, row] = selectedRegion;
                                GUI.changed = true;
                                evt.Use();
                            }
                            else if (regionPaintMode && paintingDrag && evt.type == EventType.MouseDrag && cellRect.Contains(evt.mousePosition))
                            {
                                regions[col, row] = selectedRegion;
                                GUI.changed = true;
                                evt.Use();
                            }
                            else if (regionPaintMode && paintingDrag && evt.type == EventType.MouseUp)
                            {
                                paintingDrag = false;
                                evt.Use();
                            }
                            if (GUI.Button(cellRect, label)) HandleCell(col, row);
                            GUI.backgroundColor = old;
                        }
                    }
                }
            }
            GUILayout.Space(8);
        }

        private void HandleCell(int col, int row)
        {
            if (regionPaintMode)
                regions[col, row] = selectedRegion;
            else if (clueMode)
            {
                if (answer[col, row]) clues[col, row] = !clues[col, row];
            }
            else
            {
                if (answer[col, row]) { answer[col, row] = false; clues[col, row] = false; }
                else
                {
                    for (int x = 0; x < columns; x++) { answer[x, row] = false; clues[x, row] = false; }
                    answer[col, row] = true;
                }
            }
            GUI.changed = true;
        }

        private void ValidateAnswer()
        {
            int count = Count(answer);
            var rowUsed = new bool[rows]; var colUsed = new bool[columns]; var regionUsed = new bool[rows];
            var cells = new List<Vector2Int>();
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
                if (answer[x, y]) cells.Add(new Vector2Int(x, y));
            string error = null;
            foreach (var cell in cells)
            {
                int region = regions[cell.x, cell.y];
                if (rowUsed[cell.y]) error = "Only one cat is allowed in each row.";
                else if (colUsed[cell.x]) error = "Two cats use the same column.";
                else if (regionUsed[region]) error = "Two cats use the same colored region.";
                rowUsed[cell.y] = colUsed[cell.x] = regionUsed[region] = true;
                foreach (var other in cells)
                    if (other != cell && Mathf.Abs(other.x - cell.x) <= 1 && Mathf.Abs(other.y - cell.y) <= 1)
                        error = "Cats cannot touch, including diagonally.";
            }
            foreach (var cell in cells)
                if (clues[cell.x, cell.y] && !answer[cell.x, cell.y]) error = "Starting clues must be answer cats.";

            if (error == null && columns < rows) error = "Columns must be at least the number of rows so each cat can use a different column.";
            if (error == null && CountBoardRegions() != rows) error = "Use exactly one colored region per row. Paint the whole board with exactly " + rows + " different colors.";
            if (error == null && count == rows && CountUsedRegions() != rows) error = "Each answer cat must use a different colored region.";
            validationMessage = error ?? (count == rows ? "Looks good! All four rules are satisfied." : $"Place {rows - count} more cat{(rows - count == 1 ? "" : "s")} to complete the answer.");
            validationType = error != null ? MessageType.Error : count == rows ? MessageType.Info : MessageType.Warning;
        }

        private static int Count(bool[,] cells)
        {
            int total = 0;
            foreach (bool cell in cells) if (cell) total++;
            return total;
        }

        private void LoadLevel(LevelSO selected)
        {
            level = selected;
            ClearBoard();
            if (level == null) { levelNumber = 1; levelName = "New Level"; difficulty = LevelDifficulty.Easy; ResizeBoard(DefaultSize, DefaultSize); return; }
            levelNumber = level.LevelNumber; levelName = level.LevelName; difficulty = level.Difficulty;
            ResizeBoard(level.Rows, level.Columns);
                if (level.CellRegions != null && level.CellRegions.Length == rows * columns)
                for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++) regions[x, y] = Mathf.Clamp(level.CellRegions[y * columns + x], 0, RegionColors.Length - 1);
            else
                for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++) regions[x, y] = Mathf.Clamp((y / 3) * Mathf.Max(1, columns / 3) + x / 3, 0, RegionColors.Length - 1);
            if (level.MeowPositions != null)
                foreach (var cell in level.MeowPositions)
                    if (cell.x >= 0 && cell.x < columns && cell.y >= 0 && cell.y < rows) answer[cell.x, cell.y] = true;
            if (level.StartingMeowPositions != null)
                foreach (var cell in level.StartingMeowPositions)
                    if (cell.x >= 0 && cell.x < columns && cell.y >= 0 && cell.y < rows) clues[cell.x, cell.y] = true;
        }

        private void ClearBoard()
        {
            for (int x = 0; x < columns; x++) for (int y = 0; y < rows; y++) answer[x, y] = clues[x, y] = false;
        }

        private void SaveLevel()
        {
            if (level == null)
            {
                string path = EditorUtility.SaveFilePanelInProject("Create Meowdoku Level", $"Level_{levelNumber:000}", "asset", "Choose where to save the level.");
                if (string.IsNullOrEmpty(path)) return;
                level = CreateInstance<LevelSO>();
                AssetDatabase.CreateAsset(level, path);
            }
            var serialized = new SerializedObject(level);
            serialized.FindProperty("levelNumber").intValue = Mathf.Max(1, levelNumber);
            serialized.FindProperty("levelName").stringValue = levelName;
            serialized.FindProperty("difficulty").enumValueIndex = (int)difficulty;
            serialized.FindProperty("rows").intValue = rows;
            serialized.FindProperty("columns").intValue = columns;
            var regionIds = serialized.FindProperty("cellRegions");
            regionIds.arraySize = rows * columns;
            for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++) regionIds.GetArrayElementAtIndex(y * columns + x).intValue = regions[x, y];
            serialized.FindProperty("meowPositions").arraySize = rows;
            var starts = new List<Vector2Int>();
            int rowIndex = 0;
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
                if (answer[x, y])
                {
                    var element = serialized.FindProperty("meowPositions").GetArrayElementAtIndex(rowIndex++);
                    element.vector2IntValue = new Vector2Int(x, y);
                    if (clues[x, y]) starts.Add(new Vector2Int(x, y));
                }
            var clueArray = serialized.FindProperty("startingMeowPositions");
            clueArray.arraySize = starts.Count;
            for (int i = 0; i < starts.Count; i++) clueArray.GetArrayElementAtIndex(i).vector2IntValue = starts[i];
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
            Selection.activeObject = level;
            EditorGUIUtility.PingObject(level);
        }

        private void ResizeBoard(int newRows, int newColumns)
        {
            var oldAnswer = answer; var oldClues = clues; var oldRegions = regions;
            int oldRows = rows, oldColumns = columns;
            rows = Mathf.Clamp(newRows, 1, 15); columns = Mathf.Clamp(newColumns, 1, 15);
            answer = new bool[columns, rows]; clues = new bool[columns, rows]; regions = new int[columns, rows];
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                if (oldAnswer != null && x < oldColumns && y < oldRows) { answer[x, y] = oldAnswer[x, y]; clues[x, y] = oldClues[x, y]; regions[x, y] = Mathf.Clamp(oldRegions[x, y], 0, RegionColors.Length - 1); }
                else regions[x, y] = Mathf.Min(rows - 1, y);
            }
            selectedRegion = Mathf.Clamp(selectedRegion, 0, RegionColors.Length - 1);
        }

        private int CountUsedRegions()
        {
            var used = new bool[RegionColors.Length]; int total = 0;
            for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
                if (answer[x, y] && !used[regions[x, y]]) { used[regions[x, y]] = true; total++; }
            return total;
        }

        private int CountBoardRegions()
        {
            var used = new bool[RegionColors.Length]; int total = 0;
            for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
                if (!used[regions[x, y]]) { used[regions[x, y]] = true; total++; }
            return total;
        }
    }
}
