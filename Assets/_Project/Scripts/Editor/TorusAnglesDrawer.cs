using UnityEditor;
using UnityEngine;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>Shows <see cref="TorusAngles"/> (stored in radians) as two fields in degrees.</summary>
    [CustomPropertyDrawer(typeof(TorusAngles))]
    public class TorusAnglesDrawer : PropertyDrawer
    {
        private static readonly GUIContent[] SubLabels =
        {
            new GUIContent("Theta°", "Along the snake body, in degrees"),
            new GUIContent("Phi°", "Around the tube, in degrees. 0 = outer side, 90 = top"),
        };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var theta = property.FindPropertyRelative("thetaRadians");
            var phi = property.FindPropertyRelative("phiRadians");

            var values = new[] { theta.floatValue * Mathf.Rad2Deg, phi.floatValue * Mathf.Rad2Deg };

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            EditorGUI.MultiFloatField(position, label, SubLabels, values);
            if (EditorGUI.EndChangeCheck())
            {
                theta.floatValue = values[0] * Mathf.Deg2Rad;
                phi.floatValue = values[1] * Mathf.Deg2Rad;
            }

            EditorGUI.EndProperty();
        }
    }
}
