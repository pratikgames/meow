using UnityEngine;

namespace PuzzleGame
{
    /// <summary>Shared runtime-only visuals for the first playable prototype.</summary>
    internal static class PlaceholderVisuals
    {
        private static Sprite squareSprite;
        private static Sprite circleSprite;
        private static Sprite catSprite;
        private static Sprite wrongSprite;
        private static Material unlitMaterial;

        public static Sprite SquareSprite
        {
            get
            {
                if (squareSprite == null)
                    squareSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                return squareSprite;
            }
        }

        public static Sprite CircleSprite
        {
            get
            {
                if (circleSprite == null)
                {
                    const int textureSize = 32;
                    var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
                    {
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    var pixels = new Color[textureSize * textureSize];
                    float center = (textureSize - 1) * 0.5f;
                    float radius = textureSize * 0.46f;
                    for (int y = 0; y < textureSize; y++)
                    for (int x = 0; x < textureSize; x++)
                    {
                        float dx = x - center;
                        float dy = y - center;
                        pixels[y * textureSize + x] = dx * dx + dy * dy <= radius * radius ? Color.white : Color.clear;
                    }
                    texture.SetPixels(pixels);
                    texture.Apply(false, true);
                    circleSprite = Sprite.Create(texture, new Rect(0, 0, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
                }
                return circleSprite;
            }
        }

        public static Sprite CatSprite
        {
            get
            {
                if (catSprite != null) return catSprite;
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[size * size];
                Color ink = new Color(0.20f, 0.16f, 0.22f);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - 31.5f) / 24f;
                    float dy = (y - 29f) / 23f;
                    bool head = dx * dx + dy * dy <= 1f;
                    bool ears = InTriangle(x, y, 10, 34, 18, 59, 29, 40) || InTriangle(x, y, 34, 40, 46, 59, 54, 34);
                    if (head || ears) pixels[y * size + x] = Color.white;
                }
                for (int y = 23; y < 28; y++) for (int x = 20; x < 25; x++) pixels[y * size + x] = ink;
                for (int y = 23; y < 28; y++) for (int x = 39; x < 44; x++) pixels[y * size + x] = ink;
                for (int y = 14; y < 18; y++) for (int x = 30; x < 34; x++) pixels[y * size + x] = new Color(0.95f, 0.55f, 0.62f);
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                catSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                return catSprite;
            }
        }

        public static Sprite WrongSprite
        {
            get
            {
                if (wrongSprite != null) return wrongSprite;
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[size * size];
                for (int i = 0; i < size; i++)
                {
                    for (int thickness = -4; thickness <= 4; thickness++)
                    {
                        int a = i + thickness;
                        int b = size - 1 - i + thickness;
                        if (a >= 0 && a < size) pixels[i * size + a] = Color.white;
                        if (b >= 0 && b < size) pixels[i * size + b] = Color.white;
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                wrongSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                return wrongSprite;
            }
        }

        private static bool InTriangle(int x, int y, int ax, int ay, int bx, int by, int cx, int cy)
        {
            float d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by);
            float d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy);
            float d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
            return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
        }

        public static void Configure(SpriteRenderer renderer)
        {
            if (unlitMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null) unlitMaterial = new Material(shader);
            }

            if (unlitMaterial != null) renderer.sharedMaterial = unlitMaterial;
        }
    }
}
