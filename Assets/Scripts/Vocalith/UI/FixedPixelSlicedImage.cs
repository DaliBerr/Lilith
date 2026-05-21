using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Vocalith.UI
{
    /// <summary>
    /// Image variant that keeps 9-slice borders at a stable screen-pixel thickness while the Canvas scales.
    /// </summary>
    [AddComponentMenu("UI/Fixed Pixel Sliced Image", 11)]
    public sealed class FixedPixelSlicedImage : Image
    {
        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            Sprite activeSprite = overrideSprite != null ? overrideSprite : sprite;
            if (type != Type.Sliced || activeSprite == null || !HasBorder(activeSprite.border))
            {
                base.OnPopulateMesh(toFill);
                return;
            }

            GenerateFixedPixelSlicedSprite(toFill, activeSprite);
        }

        public static Vector4 CalculateLocalBorder(
            Vector4 spriteBorder,
            float pixelsPerUnitMultiplier,
            Rect rect,
            float canvasScaleFactor,
            Vector3 imageLossyScale,
            Vector3 canvasLossyScale)
        {
            float multiplier = Mathf.Max(0f, pixelsPerUnitMultiplier);
            float screenLeft = Mathf.Max(0f, spriteBorder.x * multiplier);
            float screenBottom = Mathf.Max(0f, spriteBorder.y * multiplier);
            float screenRight = Mathf.Max(0f, spriteBorder.z * multiplier);
            float screenTop = Mathf.Max(0f, spriteBorder.w * multiplier);

            float xPixelsPerLocalUnit = Mathf.Max(0.0001f, canvasScaleFactor * SafeScaleRatio(imageLossyScale.x, canvasLossyScale.x));
            float yPixelsPerLocalUnit = Mathf.Max(0.0001f, canvasScaleFactor * SafeScaleRatio(imageLossyScale.y, canvasLossyScale.y));

            Vector4 localBorder = new(
                screenLeft / xPixelsPerLocalUnit,
                screenBottom / yPixelsPerLocalUnit,
                screenRight / xPixelsPerLocalUnit,
                screenTop / yPixelsPerLocalUnit);

            return ClampBorderToRect(localBorder, rect);
        }

        public static Vector4 ClampBorderToRect(Vector4 localBorder, Rect rect)
        {
            float width = Mathf.Max(0f, rect.width);
            float height = Mathf.Max(0f, rect.height);

            float horizontal = localBorder.x + localBorder.z;
            if (horizontal > width && horizontal > 0f)
            {
                float factor = width / horizontal;
                localBorder.x *= factor;
                localBorder.z *= factor;
            }

            float vertical = localBorder.y + localBorder.w;
            if (vertical > height && vertical > 0f)
            {
                float factor = height / vertical;
                localBorder.y *= factor;
                localBorder.w *= factor;
            }

            return localBorder;
        }

        private void GenerateFixedPixelSlicedSprite(VertexHelper toFill, Sprite activeSprite)
        {
            toFill.Clear();

            Rect rect = GetPixelAdjustedRect();
            Vector4 border = CalculateLocalBorder(
                activeSprite.border,
                pixelsPerUnitMultiplier,
                rect,
                canvas != null ? canvas.scaleFactor : 1f,
                rectTransform.lossyScale,
                canvas != null ? canvas.transform.lossyScale : Vector3.one);

            Vector4 outer = DataUtility.GetOuterUV(activeSprite);
            Vector4 inner = DataUtility.GetInnerUV(activeSprite);

            float[] x = { rect.xMin, rect.xMin + border.x, rect.xMax - border.z, rect.xMax };
            float[] y = { rect.yMin, rect.yMin + border.y, rect.yMax - border.w, rect.yMax };
            float[] u = { outer.x, inner.x, inner.z, outer.z };
            float[] v = { outer.y, inner.y, inner.w, outer.w };

            for (int xi = 0; xi < 3; xi++)
            {
                for (int yi = 0; yi < 3; yi++)
                {
                    if (!fillCenter && xi == 1 && yi == 1)
                    {
                        continue;
                    }

                    AddQuad(toFill, x[xi], y[yi], x[xi + 1], y[yi + 1], u[xi], v[yi], u[xi + 1], v[yi + 1]);
                }
            }
        }

        private void AddQuad(VertexHelper toFill, float xMin, float yMin, float xMax, float yMax, float uMin, float vMin, float uMax, float vMax)
        {
            if (Mathf.Approximately(xMin, xMax) || Mathf.Approximately(yMin, yMax))
            {
                return;
            }

            Color32 vertexColor = color;
            UIVertex[] quad =
            {
                CreateVertex(new Vector3(xMin, yMin), vertexColor, new Vector2(uMin, vMin)),
                CreateVertex(new Vector3(xMin, yMax), vertexColor, new Vector2(uMin, vMax)),
                CreateVertex(new Vector3(xMax, yMax), vertexColor, new Vector2(uMax, vMax)),
                CreateVertex(new Vector3(xMax, yMin), vertexColor, new Vector2(uMax, vMin)),
            };
            toFill.AddUIVertexQuad(quad);
        }

        private static UIVertex CreateVertex(Vector3 position, Color32 vertexColor, Vector2 uv)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = vertexColor;
            vertex.uv0 = uv;
            return vertex;
        }

        private static bool HasBorder(Vector4 border)
        {
            return border.x > 0f || border.y > 0f || border.z > 0f || border.w > 0f;
        }

        private static float SafeScaleRatio(float imageScale, float canvasScale)
        {
            return Mathf.Abs(imageScale) / Mathf.Max(0.0001f, Mathf.Abs(canvasScale));
        }
    }
}
