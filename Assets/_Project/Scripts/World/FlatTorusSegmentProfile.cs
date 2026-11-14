using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>Segment without terrain. Used where the world has no profile assigned.</summary>
    public class FlatTorusSegmentProfile : TorusSegmentProfile
    {
        private static FlatTorusSegmentProfile instance;
        public static FlatTorusSegmentProfile Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = CreateInstance<FlatTorusSegmentProfile>();
                    instance.hideFlags = HideFlags.HideAndDontSave;
                }

                return instance;
            }
        }

        public override float SampleHeight(float along, float around, Vector3 basePoint)
        {
            return 0f;
        }
    }
}
