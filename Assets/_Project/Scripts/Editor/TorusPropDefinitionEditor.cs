using UnityEditor;
using UnityEngine;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Inspector of <see cref="TorusPropDefinition"/> with a preview: the mesh of the prop and a red disc with a ring that shows
    /// the blocking radius, lying on the ground at the pivot of the prop (the area nobody can enter). Drag in the preview to orbit.
    /// </summary>
    [CustomEditor(typeof(TorusPropDefinition))]
    public class TorusPropDefinitionEditor : UnityEditor.Editor
    {
        private const int RingSegments = 64;
        private const float GroundLift = 0.01f;
        private const float DragSpeed = 0.5f;

        private static readonly Color DiscColor = new Color(1f, 0.2f, 0.2f, 0.18f);
        private static readonly Color RingColor = new Color(1f, 0.25f, 0.2f, 1f);

        private PreviewRenderUtility previewUtility;
        private Material meshMaterial;
        private Material areaMaterial;
        private Mesh shadedMesh;
        private Mesh sourceMesh;
        private Mesh areaMesh;
        private float areaRadius = -1f;
        private Vector2 orbit = new Vector2(135f, -25f);

        public override bool HasPreviewGUI()
        {
            return true;
        }

        public override GUIContent GetPreviewTitle()
        {
            return new GUIContent("Prop and blocking radius");
        }

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            var definition = (TorusPropDefinition)target;
            if (definition.Mesh == null)
            {
                GUI.Label(rect, "Set the mesh to see the preview.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            HandleOrbit(rect);

            if (Event.current.type != EventType.Repaint)
                return;

            EnsureResources(definition);
            var bounds = definition.Mesh.bounds;
            float extent = Mathf.Max(bounds.extents.magnitude, definition.BlockRadius * 1.15f, 0.25f);

            previewUtility.BeginPreview(rect, background);
            var camera = previewUtility.camera;
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = extent * 20f;

            var center = new Vector3(0f, bounds.center.y, 0f);
            var rotation = Quaternion.Euler(orbit.y, orbit.x, 0f);
            float distance = extent / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
            camera.transform.SetPositionAndRotation(center - rotation * Vector3.forward * distance, rotation);

            previewUtility.DrawMesh(shadedMesh, Matrix4x4.identity, meshMaterial, 0);
            previewUtility.DrawMesh(areaMesh, Matrix4x4.identity, areaMaterial, 0);
            camera.Render();

            var texture = previewUtility.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }

        // Dragging with the mouse inside the preview rotates the view (PreviewGUI.Drag2D is internal, so it is done by hand).
        private void HandleOrbit(Rect rect)
        {
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            var current = Event.current;
            switch (current.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (rect.Contains(current.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        current.Use();
                    }

                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        current.Use();
                    }

                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        orbit.x -= current.delta.x * DragSpeed;
                        orbit.y = Mathf.Clamp(orbit.y - current.delta.y * DragSpeed, -89f, 89f);
                        current.Use();
                        Repaint();
                    }

                    break;
            }
        }

        private void EnsureResources(TorusPropDefinition definition)
        {
            if (previewUtility == null)
            {
                previewUtility = new PreviewRenderUtility();
                previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
            }

            // Unlit materials that show vertex colors, so the preview does not depend on the render pipeline.
            if (meshMaterial == null)
            {
                meshMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                meshMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                meshMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                meshMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                meshMaterial.SetInt("_ZWrite", 1);
            }

            if (areaMaterial == null)
                areaMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };

            if (shadedMesh == null || sourceMesh != definition.Mesh)
            {
                DestroyMesh(ref shadedMesh);
                sourceMesh = definition.Mesh;
                shadedMesh = CreateShadedMesh(sourceMesh);
            }

            if (areaMesh == null || Mathf.Approximately(areaRadius, definition.BlockRadius) == false)
            {
                DestroyMesh(ref areaMesh);
                areaRadius = definition.BlockRadius;
                areaMesh = CreateAreaMesh(areaRadius);
            }
        }

        // A copy of the mesh with its vertex colors darkened by a fixed light, so the shape is readable without any lighting.
        private static Mesh CreateShadedMesh(Mesh source)
        {
            var copy = Instantiate(source);
            copy.hideFlags = HideFlags.HideAndDontSave;
            if (source.isReadable == false)
                return copy;

            var normals = source.normals;
            var colors = source.colors;
            var shaded = new Color[source.vertexCount];
            var lightDirection = new Vector3(0.4f, 0.8f, -0.5f).normalized;
            for (int i = 0; i < shaded.Length; i++)
            {
                var baseColor = i < colors.Length ? colors[i] : Color.white;
                float light = i < normals.Length ? 0.55f + 0.45f * Mathf.Max(0f, Vector3.Dot(normals[i], lightDirection)) : 1f;
                shaded[i] = new Color(baseColor.r * light, baseColor.g * light, baseColor.b * light, 1f);
            }

            copy.colors = shaded;
            return copy;
        }

        // A flat disc with a ring on its edge, lying on the ground (the XZ plane) around the pivot of the prop.
        private static Mesh CreateAreaMesh(float radius)
        {
            var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            if (radius <= 0f)
                return mesh;

            float thickness = Mathf.Max(0.02f, radius * 0.03f);
            var vertices = new Vector3[1 + RingSegments + RingSegments * 2];
            var colors = new Color[vertices.Length];
            var triangles = new int[RingSegments * 3 + RingSegments * 6];

            vertices[0] = new Vector3(0f, GroundLift, 0f);
            colors[0] = DiscColor;
            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / RingSegments;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                vertices[1 + i] = direction * radius + Vector3.up * GroundLift;
                colors[1 + i] = DiscColor;

                int ring = 1 + RingSegments + i * 2;
                vertices[ring] = direction * (radius - thickness) + Vector3.up * (GroundLift * 2f);
                vertices[ring + 1] = direction * (radius + thickness) + Vector3.up * (GroundLift * 2f);
                colors[ring] = RingColor;
                colors[ring + 1] = RingColor;

                int next = (i + 1) % RingSegments;
                int nextRing = 1 + RingSegments + next * 2;

                int fan = i * 3;
                triangles[fan] = 0;
                triangles[fan + 1] = 1 + next;
                triangles[fan + 2] = 1 + i;

                int quad = RingSegments * 3 + i * 6;
                triangles[quad] = ring;
                triangles[quad + 1] = nextRing;
                triangles[quad + 2] = ring + 1;
                triangles[quad + 3] = ring + 1;
                triangles[quad + 4] = nextRing;
                triangles[quad + 5] = nextRing + 1;
            }

            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void DestroyMesh(ref Mesh mesh)
        {
            if (mesh)
                DestroyImmediate(mesh);

            mesh = null;
        }

        private void OnDisable()
        {
            if (previewUtility != null)
                previewUtility.Cleanup();

            previewUtility = null;
            DestroyMesh(ref shadedMesh);
            DestroyMesh(ref areaMesh);
            if (meshMaterial)
                DestroyImmediate(meshMaterial);

            if (areaMaterial)
                DestroyImmediate(areaMaterial);
        }
    }
}
