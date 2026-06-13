using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Vocalith.UI
{
    /// <summary>
    /// Draws a simple sprite like CSS background-size: cover, filling the rect without stretching.
    /// </summary>
    [AddComponentMenu("UI/Cover Image", 12)]
    public sealed class CoverImage : Image
    {
        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            Sprite activeSprite = overrideSprite != null ? overrideSprite : sprite;
            if (activeSprite == null || type != Type.Simple)
            {
                base.OnPopulateMesh(toFill);
                return;
            }

            GenerateCoverSprite(toFill, activeSprite);
        }

        public static Vector4 CalculateCoverUv(Vector4 outerUv, float spriteAspect, float rectAspect)
        {
            if (spriteAspect <= 0f || rectAspect <= 0f)
            {
                return outerUv;
            }

            float uMin = outerUv.x;
            float vMin = outerUv.y;
            float uMax = outerUv.z;
            float vMax = outerUv.w;

            if (rectAspect < spriteAspect)
            {
                float visibleWidth = rectAspect / spriteAspect;
                float crop = (1f - visibleWidth) * 0.5f;
                float uvWidth = uMax - uMin;
                uMin += uvWidth * crop;
                uMax -= uvWidth * crop;
            }
            else if (rectAspect > spriteAspect)
            {
                float visibleHeight = spriteAspect / rectAspect;
                float crop = (1f - visibleHeight) * 0.5f;
                float uvHeight = vMax - vMin;
                vMin += uvHeight * crop;
                vMax -= uvHeight * crop;
            }

            return new Vector4(uMin, vMin, uMax, vMax);
        }

        private void GenerateCoverSprite(VertexHelper toFill, Sprite activeSprite)
        {
            toFill.Clear();

            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            float spriteAspect = activeSprite.rect.width / Mathf.Max(0.0001f, activeSprite.rect.height);
            float rectAspect = rect.width / Mathf.Max(0.0001f, rect.height);
            Vector4 uv = CalculateCoverUv(DataUtility.GetOuterUV(activeSprite), spriteAspect, rectAspect);
            AddQuad(toFill, rect, uv);
        }

        private void AddQuad(VertexHelper toFill, Rect rect, Vector4 uv)
        {
            Color32 vertexColor = color;
            UIVertex[] quad =
            {
                CreateVertex(new Vector3(rect.xMin, rect.yMin), vertexColor, new Vector2(uv.x, uv.y)),
                CreateVertex(new Vector3(rect.xMin, rect.yMax), vertexColor, new Vector2(uv.x, uv.w)),
                CreateVertex(new Vector3(rect.xMax, rect.yMax), vertexColor, new Vector2(uv.z, uv.w)),
                CreateVertex(new Vector3(rect.xMax, rect.yMin), vertexColor, new Vector2(uv.z, uv.y)),
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
    }
}
