using System.Collections.Generic;
using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// The pseudo colliders of the props: circles on the surface of the torus that can not be entered.
    /// Objects moved by <see cref="TorusObject"/> are pushed out of them, so they slide along the edge.
    /// A grid of cells makes the lookup cheap even with tens of thousands of props.
    /// </summary>
    public class TorusPropObstacles
    {
        private const float CellSize = 8f;

        private readonly struct Obstacle
        {
            public readonly TorusAngles Angles;
            public readonly float Radius;

            public Obstacle(TorusAngles angles, float radius)
            {
                Angles = angles;
                Radius = radius;
            }
        }

        private readonly List<Obstacle> obstacles = new List<Obstacle>();
        private List<int>[] cells;
        private int cellsAlong;
        private int cellsAround;

        public int Count => obstacles.Count;

        public void Clear()
        {
            obstacles.Clear();
            cells = null;
        }

        /// <summary>Adds an obstacle. Call <see cref="Build"/> after adding all of them.</summary>
        public void Add(TorusAngles angles, float radius)
        {
            if (radius > 0f)
                obstacles.Add(new Obstacle(angles, Mathf.Min(radius, CellSize * 0.5f)));
        }

        /// <summary>Sorts the obstacles into the grid cells.</summary>
        public void Build(TorusSurface surface)
        {
            cellsAlong = Mathf.Max(1, Mathf.FloorToInt(TorusGeometry.TwoPi * surface.MajorRadius / CellSize));
            cellsAround = Mathf.Max(1, Mathf.FloorToInt(TorusGeometry.TwoPi * surface.MinorRadius / CellSize));
            cells = new List<int>[cellsAlong * cellsAround];

            for (int i = 0; i < obstacles.Count; i++)
            {
                int index = GetCellIndex(GetCellAlong(obstacles[i].Angles.Theta), GetCellAround(obstacles[i].Angles.Phi));
                cells[index] ??= new List<int>();
                cells[index].Add(i);
            }
        }

        /// <summary>Pushes the position out of every obstacle it is inside of. <paramref name="bodyRadius"/> is the radius of the object.</summary>
        public TorusAngles Resolve(TorusSurface surface, TorusAngles angles, float bodyRadius)
        {
            if (cells == null || obstacles.Count == 0)
                return angles;

            int centerAlong = GetCellAlong(angles.Theta);
            int centerAround = GetCellAround(angles.Phi);
            for (int along = -1; along <= 1; along++)
            {
                for (int around = -1; around <= 1; around++)
                {
                    var cell = cells[GetCellIndex(centerAlong + along, centerAround + around)];
                    if (cell == null)
                        continue;

                    foreach (var index in cell)
                        angles = PushOut(surface, angles, obstacles[index], bodyRadius);
                }
            }

            return angles;
        }

        private static TorusAngles PushOut(TorusSurface surface, TorusAngles angles, Obstacle obstacle, float bodyRadius)
        {
            var delta = obstacle.Angles.DeltaTo(angles);
            float ring = Mathf.Max(0.1f, TorusGeometry.GetRingRadius(surface.MajorRadius, surface.MinorRadius, obstacle.Angles.Phi));
            var offset = new Vector2(delta.x * ring, delta.y * surface.MinorRadius);

            float minDistance = obstacle.Radius + bodyRadius;
            float distance = offset.magnitude;
            if (distance >= minDistance)
                return angles;

            var direction = distance > 0.0001f ? offset / distance : Vector2.right;
            var pushed = direction * minDistance;
            return obstacle.Angles.Add(pushed.x / ring, pushed.y / surface.MinorRadius);
        }

        private int GetCellAlong(float theta)
        {
            return Mathf.FloorToInt(theta / TorusGeometry.TwoPi * cellsAlong) % cellsAlong;
        }

        private int GetCellAround(float phi)
        {
            return Mathf.FloorToInt(phi / TorusGeometry.TwoPi * cellsAround) % cellsAround;
        }

        private int GetCellIndex(int along, int around)
        {
            return ((along % cellsAlong + cellsAlong) % cellsAlong) * cellsAround + (around % cellsAround + cellsAround) % cellsAround;
        }
    }
}
