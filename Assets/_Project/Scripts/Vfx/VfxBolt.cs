using UnityEngine;
using UnityEngine.Rendering;

namespace WSFGameJam2026.Vfx
{
    /// <summary>
    /// Lightning (port of the LD57 MageBoltFx): a zigzag of stretched crystals (three side by side) and a bright line with
    /// two forks. Two passes (frames 0 and 2) with a new zigzag, so it flickers.
    /// </summary>
    public class VfxBolt : MonoBehaviour
    {
        private const float MainWidth = 0.25f;
        private const float ForkWidth = 0.11f;
        private const int ForkCount = 2;

        private Vector3 start;
        private Vector3 end;
        private Vector3 worldUp;
        private Color color;
        private float thickness;
        private float duration;
        private int frame;
        private LineRenderer mainLine;
        private LineRenderer[] forks;

        public void Play(Vector3 from, Vector3 to, Vector3 up, Color newColor, float newThickness, float seconds)
        {
            start = from;
            end = to;
            worldUp = up;
            color = newColor;
            thickness = newThickness;
            duration = Mathf.Max(0.06f, seconds);

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

            mainLine = CreateLine(gameObject, MainWidth, gradient);
            forks = new LineRenderer[ForkCount];
            for (int i = 0; i < ForkCount; i++)
            {
                var forkObject = new GameObject("Fork");
                forkObject.transform.SetParent(transform, false);
                forks[i] = CreateLine(forkObject, ForkWidth, gradient);
            }

            EmitPath(duration * 0.55f);
        }

        private void Update()
        {
            frame++;
            // Flicker over three frames: dim, full with a new zigzag, dim.
            float width = frame == 2 ? 1f : frame == 1 ? 0.5f : 0.65f;
            mainLine.widthMultiplier = MainWidth * width;
            for (int i = 0; i < forks.Length; i++)
                forks[i].widthMultiplier = ForkWidth * width;

            if (frame == 2)
                EmitPath(duration * 0.45f);

            if (frame > 3)
                Destroy(gameObject);
        }

        private static LineRenderer CreateLine(GameObject owner, float width, Gradient gradient)
        {
            var line = owner.AddComponent<LineRenderer>();
            line.sharedMaterial = VfxMaterials.Line;
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.colorGradient = gradient;
            line.positionCount = 0;
            return line;
        }

        private void EmitPath(float life)
        {
            float distance = Vector3.Distance(start, end);
            int count = Mathf.Clamp(Mathf.RoundToInt(distance / 0.45f), 8, 14);
            var axis = distance > 0.01f ? (end - start) / distance : Vector3.forward;
            var side = Vector3.Cross(axis, worldUp);
            side = side.sqrMagnitude < 0.01f ? Vector3.Cross(axis, Vector3.right).normalized : side.normalized;
            var up = Vector3.Cross(side, axis).normalized;

            var points = new Vector3[count + 1];
            for (int i = 0; i <= count; i++)
            {
                var point = Vector3.Lerp(start, end, i / (float)count);
                if (i > 0 && i < count)
                    point += side * Random.Range(-0.32f, 0.32f) + up * Random.Range(-0.22f, 0.22f);

                points[i] = point;
            }

            mainLine.positionCount = points.Length;
            mainLine.SetPositions(points);
            for (int i = 0; i < forks.Length; i++)
                SetFork(forks[i], points, side, up, axis);

            EmitCrystals(points, up, life);
        }

        private static void SetFork(LineRenderer fork, Vector3[] points, Vector3 side, Vector3 up, Vector3 axis)
        {
            int count = points.Length - 1;
            if (count < 4)
            {
                fork.positionCount = 0;
                return;
            }

            int origin = Random.Range(1, count - 1);
            var direction = (side * Random.Range(-1f, 1f) + up * Random.Range(-0.6f, 0.6f) + axis * 0.5f).normalized;
            int length = Random.Range(3, 5);
            fork.positionCount = length;
            var point = points[origin];
            for (int i = 0; i < length; i++)
            {
                fork.SetPosition(i, point);
                point += direction * 0.5f;
                direction = (direction + Random.insideUnitSphere * 0.6f).normalized;
            }
        }

        private void EmitCrystals(Vector3[] points, Vector3 up, float life)
        {
            float third = thickness / 3f;
            var core = Color.Lerp(color, Color.white, 0.8f);
            for (int i = 0; i < points.Length - 1; i++)
            {
                var segment = points[i + 1] - points[i];
                float length = segment.magnitude;
                if (length < 0.01f)
                    continue;

                var rotation = Quaternion.LookRotation(segment / length, up);
                var middle = (points[i] + points[i + 1]) * 0.5f;
                var size = new Vector3(third * 1.3f, third * 1.3f, length * 1.25f);
                var sideOffset = rotation * Vector3.right * third;
                VfxParticles.Crystal(middle, core, size, life, rotation);
                VfxParticles.Crystal(middle + sideOffset, color, size, life, rotation);
                VfxParticles.Crystal(middle - sideOffset, color, size, life, rotation);
                if (i > 0 && Random.value < 0.35f)
                    EmitBranch(points[i], rotation, up, third, life);
            }
        }

        private void EmitBranch(Vector3 point, Quaternion segmentRotation, Vector3 up, float third, float life)
        {
            var direction = (segmentRotation * new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0.4f)).normalized;
            for (int i = 0; i < 3; i++)
            {
                float size = third * (1f - i * 0.2f);
                VfxParticles.Crystal(point + direction * 0.15f, color, new Vector3(size, size, 0.32f), life * (1f - i * 0.2f),
                    Quaternion.LookRotation(direction, up));
                point += direction * 0.3f;
                direction = (direction + Random.insideUnitSphere * 0.5f).normalized;
            }
        }
    }
}
