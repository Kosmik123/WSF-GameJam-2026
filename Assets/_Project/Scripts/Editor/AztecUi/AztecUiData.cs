using UnityEditor;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Head;
using WSFGameJam2026.Player;
using WSFGameJam2026.Progression;
using WSFGameJam2026.Rounds;
using WSFGameJam2026.Skills;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor.AztecUi
{
    /// <summary>
    /// Everything the UI needs besides the UI itself: Polish names and descriptions of the classes and skills
    /// (the UI is in Polish), the accent colors of the skills, and the components that feed the screens
    /// (lap, level, run statistics, waiting for "start again" after a death).
    /// </summary>
    internal static class AztecUiData
    {
        private const string SkillsFolder = "Assets/_Project/Data/Skills";
        private const string ClassesFolder = "Assets/_Project/Data/Classes";

        private static readonly SkillText[] Skills =
        {
            new SkillText("Mage/Fireball", "Kula ognia", "#E3A23B", "Ewolucja: Pożoga",
                "Leci prosto i wybucha: 24 obrażeń w promieniu 2 m.",
                "Wybuch zadaje 32 obrażeń.",
                "Wybuch zadaje 40 obrażeń.",
                "Ziemia po wybuchu płonie 1,5 s i trzy razy rani wrogów."),
            new SkillText("Mage/FrostNova", "Lodowa nova", "#8EC3EA", "Ewolucja: Szron",
                "Pierścień lodu w promieniu 3 m: 12 obrażeń i 2 s spowolnienia.",
                "Promień 3,5 m, 16 obrażeń.",
                "Promień 4 m, 20 obrażeń.",
                "Wrogowie w pierścieniu zamarzają na 1,5 s."),
            new SkillText("Mage/ChainLightning", "Łańcuch błyskawic", "#C9B8F0", "Ewolucja: Burza",
                "Trafia najbliższego wroga (8 m) i przeskakuje na 3 kolejnych: 16 obrażeń, każdy skok o 4 mniej.",
                "4 skoki, 20 obrażeń.",
                "5 skoków, 24 obrażenia.",
                "Każde trafienie ogłusza wroga na 0,5 s."),
            new SkillText("Mage/Blink", "Teleport", "#EDE6D6", "Ewolucja: Szczelina",
                "Skok o 5 m w kierunku ruchu, 0,3 s nietykalności i wybuch na końcu (12 obrażeń).",
                "Skok o 6 m.",
                "Skok o 7 m.",
                "Wybuch także w miejscu startu."),
            new SkillText("Mage/ArcaneOrbs", "Magiczne kule", "#B9A7EE", "Ewolucja: Strażnicy",
                "3 kule krążą wokół ciebie przez 6 s i ranią wrogów przy dotyku (8 obrażeń).",
                "4 kule, 12 obrażeń.",
                "5 kul, 16 obrażeń.",
                "Co sekundę jedna z kul strzela w najbliższego wroga."),
            new SkillText("Warrior/Spin", "Wir", "#36B3A0", "Ewolucja: Tornado",
                "Wir mieczem: 8 obrażeń co 0,25 s w promieniu 2,5 m przez 1 s.",
                "Promień 3 m, 12 obrażeń na uderzenie.",
                "Promień 3,5 m, 16 obrażeń na uderzenie.",
                "Wir trwa 2 s i przyciąga wrogów do środka."),
            new SkillText("Warrior/Charge", "Szarża", "#FF8A7F", "Ewolucja: Ognista szarża",
                "Zryw o 6 m przez wrogów, 20 obrażeń, nietykalność w trakcie.",
                "Zryw o 8 m.",
                "28 obrażeń.",
                "Zostawia ognisty ślad na 3 s (8 obrażeń na sekundę)."),
            new SkillText("Warrior/GroundSlam", "Uderzenie w ziemię", "#C9A24A", "Ewolucja: Rozpadlina",
                "16 obrażeń w promieniu 3 m i odrzut o 3 m.",
                "Promień 4 m.",
                "24 obrażenia.",
                "Dodatkowo ogłusza wrogów na 1 s."),
            new SkillText("Warrior/AxeThrow", "Rzut toporem", "#D9A066", "Ewolucja: Dwa topory",
                "Topór leci 10 m i wraca: 12 obrażeń w każdą stronę.",
                "Zasięg 13 m.",
                "16 obrażeń.",
                "Rzut dwoma toporami rozchylonymi o 15°."),
            new SkillText("Warrior/WarCry", "Okrzyk bojowy", "#E0483C", "Ewolucja: Ryk",
                "Odpycha wrogów w promieniu 4 m o 4 m i ogłusza ich na 1 s.",
                "Promień 5 m.",
                "Ogłuszenie 1,5 s.",
                "Promień 6 m i 12 obrażeń."),
        };

        private static readonly string[] TierNames = { "Baza", "Ulepszenie I", "Ulepszenie II" };

        public static void TranslateContent()
        {
            foreach (var skill in Skills)
                skill.Apply();

            SetClass("Mage", "Mag", "Strzela z dystansu, 80 HP. Kula ognia, lodowa nova, łańcuch błyskawic, teleport.");
            SetClass("Warrior", "Wojownik", "Miecz, tarcza i unik, 100 HP. Wir, szarża, uderzenie w ziemię, rzut toporem.");
            AssetDatabase.SaveAssets();
        }

        /// <summary>Adds the components that feed the screens and connects them. Returns the lap tracker.</summary>
        public static LapTracker SetUpGameplayComponents(RoundController controller, out PlayerLevel playerLevel, out RunStats runStats)
        {
            var playerCombat = Object.FindAnyObjectByType<PlayerCombat>();
            var playerHealth = playerCombat.GetComponent<Health>();

            var lapTracker = Object.FindAnyObjectByType<LapTracker>();
            if (lapTracker == null)
                lapTracker = Undo.AddComponent<LapTracker>(controller.gameObject);

            Set(lapTracker, "head", Object.FindAnyObjectByType<HeadMovement>());
            Set(lapTracker, "player", playerCombat.GetComponent<TorusObject>());
            Set(lapTracker, "roundController", controller);

            if (playerCombat.TryGetComponent(out playerLevel) == false)
                playerLevel = Undo.AddComponent<PlayerLevel>(playerCombat.gameObject);

            Set(playerLevel, "roundController", controller);

            runStats = Object.FindAnyObjectByType<RunStats>();
            if (runStats == null)
                runStats = Undo.AddComponent<RunStats>(controller.gameObject);

            Set(runStats, "roundController", controller);
            Set(runStats, "lapTracker", lapTracker);
            Set(runStats, "playerHealth", playerHealth);

            // The death screen has its own buttons, so the run waits for "start again".
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("restartAutomatically").boolValue = false;
            serializedController.ApplyModifiedProperties();
            return lapTracker;
        }

        public static void Set(Object target, string propertyName, Object value)
        {
            if (target == null)
                return;

            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }

        private static void SetClass(string fileName, string displayName, string description)
        {
            var playerClass = AssetDatabase.LoadAssetAtPath<PlayerClass>($"{ClassesFolder}/{fileName}.asset");
            if (playerClass == null)
                return;

            var serialized = new SerializedObject(playerClass);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("description").stringValue = description;
            serialized.ApplyModifiedProperties();
        }

        private readonly struct SkillText
        {
            private readonly string path;
            private readonly string displayName;
            private readonly string accentColor;
            private readonly string evolutionName;
            private readonly string[] descriptions;

            public SkillText(string path, string displayName, string accentColor, string evolutionName, params string[] descriptions)
            {
                this.path = path;
                this.displayName = displayName;
                this.accentColor = accentColor;
                this.evolutionName = evolutionName;
                this.descriptions = descriptions;
            }

            public void Apply()
            {
                var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>($"{SkillsFolder}/{path}.asset");
                if (skill == null)
                    return;

                var serialized = new SerializedObject(skill);
                serialized.FindProperty("displayName").stringValue = displayName;
                if (ColorUtility.TryParseHtmlString(accentColor, out var color))
                    serialized.FindProperty("accentColor").colorValue = color;

                var tiers = serialized.FindProperty("tiers");
                for (int i = 0; i < tiers.arraySize && i < descriptions.Length; i++)
                {
                    var tier = tiers.GetArrayElementAtIndex(i);
                    tier.FindPropertyRelative("tierName").stringValue = i < TierNames.Length ? TierNames[i] : evolutionName;
                    tier.FindPropertyRelative("description").stringValue = descriptions[i];
                }

                serialized.ApplyModifiedProperties();
            }
        }
    }
}
