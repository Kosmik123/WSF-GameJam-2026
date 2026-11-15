using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using WSFGameJam2026.Enemies;
using WSFGameJam2026.Head;
using WSFGameJam2026.Hud;
using WSFGameJam2026.Menu;
using WSFGameJam2026.Player;
using WSFGameJam2026.Rounds;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Adds the main menu to the open scene (the menu and the game are one scene): a canvas with the Play button in the centre
    /// and the <see cref="MainMenu"/> component, connected to the camera that looks at the whole torus ("Menu VCam"), to the camera
    /// of the player (the Cinemachine camera under the player), to the round loop and to the head. The player is parked in the menu,
    /// the round loop no longer starts by itself. Needs the player, the head, the round loop and two Cinemachine cameras.
    /// Safe to run again.
    /// </summary>
    public static class MainMenuSetupMenu
    {
        private const string MenuCameraName = "Menu VCam";

        [MenuItem("WSF GameJam/Setup/Create Main Menu")]
        private static void CreateMainMenu()
        {
            var player = Object.FindAnyObjectByType<TorusPlayerMovement>();
            var head = Object.FindAnyObjectByType<HeadMovement>();
            var controller = Object.FindAnyObjectByType<RoundController>();
            if (player == null || head == null || controller == null)
            {
                EditorUtility.DisplayDialog("Main menu setup", "The scene needs the player, the head and the round loop (WSF GameJam/Setup/Create Round Loop).", "OK");
                return;
            }

            if (TMP_Settings.instance == null)
            {
                HudSetupMenu.AskToImportTextMeshPro();
                return;
            }

            var gameCamera = player.GetComponentInChildren<CinemachineCamera>(true);
            CinemachineCamera menuCamera = null;
            foreach (var candidate in Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate != gameCamera && (menuCamera == null || candidate.name == MenuCameraName))
                    menuCamera = candidate;
            }

            if (gameCamera == null || menuCamera == null)
            {
                EditorUtility.DisplayDialog("Main menu setup", $"Needs two Cinemachine cameras: one under the player (his camera) and \"{MenuCameraName}\" (looking at the torus from above).", "OK");
                return;
            }

            var menu = Object.FindAnyObjectByType<MainMenu>();
            Button playButton;
            GameObject screen;
            if (menu == null)
            {
                var menuObject = new GameObject("Main Menu");
                Undo.RegisterCreatedObjectUndo(menuObject, "Create Main Menu");
                menu = menuObject.AddComponent<MainMenu>();
                screen = CreateScreen(out playButton);
            }
            else
            {
                var existing = new SerializedObject(menu);
                playButton = (Button)existing.FindProperty("playButton").objectReferenceValue;
                screen = (GameObject)existing.FindProperty("menuScreen").objectReferenceValue;
            }

            EnsureEventSystem();

            // The round loop waits for the Play button.
            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("startAutomatically").boolValue = false;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            var serialized = new SerializedObject(menu);
            serialized.FindProperty("playButton").objectReferenceValue = playButton;
            serialized.FindProperty("menuScreen").objectReferenceValue = screen;
            serialized.FindProperty("menuCamera").objectReferenceValue = menuCamera;
            serialized.FindProperty("gameCamera").objectReferenceValue = gameCamera;
            serialized.FindProperty("controller").objectReferenceValue = controller;
            serialized.FindProperty("head").objectReferenceValue = head;
            SetArray(serialized.FindProperty("hiddenInMenu"), FindHiddenObjects(player));
            SetArray(serialized.FindProperty("pausedInMenu"), FindPausedBehaviours(player));
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            Selection.activeObject = menu.gameObject;
            Debug.Log("Main menu setup done: the player is parked in the menu and the Play button starts the game. Save the scene to keep it.");
        }

        // The model of the player (every child of his except the one with his camera) and the HUD.
        private static List<Object> FindHiddenObjects(TorusPlayerMovement player)
        {
            var hidden = new List<Object>();
            foreach (Transform child in player.transform)
            {
                if (child.GetComponentInChildren<CinemachineCamera>(true) == null)
                    hidden.Add(child.gameObject);
            }

            var healthBar = Object.FindAnyObjectByType<HealthBar>();
            if (healthBar)
                hidden.Add(healthBar.GetComponentInParent<Canvas>().gameObject);

            return hidden;
        }

        // Everything on the player that acts (movement, combat, health: a disabled Health can not be hit) except the TorusObject, plus the enemy spawner.
        private static List<Object> FindPausedBehaviours(TorusPlayerMovement player)
        {
            var paused = new List<Object>();
            foreach (var behaviour in player.GetComponents<Behaviour>())
            {
                if (behaviour is TorusObject == false)
                    paused.Add(behaviour);
            }

            var spawner = Object.FindAnyObjectByType<EnemySpawner>();
            if (spawner)
                paused.Add(spawner);

            return paused;
        }

        private static void SetArray(SerializedProperty array, List<Object> values)
        {
            array.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static GameObject CreateScreen(out Button playButton)
        {
            var canvasObject = new GameObject("Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Main Menu");
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var buttonObject = new GameObject("Play Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvasObject.transform, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(360f, 120f);
            rect.anchoredPosition = Vector2.zero;
            buttonObject.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

            HudSetupMenu.CreateText("Label", buttonObject.transform, 56f, TextAlignmentOptions.Center, "Play");

            playButton = buttonObject.GetComponent<Button>();
            return canvasObject;
        }

        // The buttons need an event system that understands the Input System.
        private static void EnsureEventSystem()
        {
            var eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                eventSystem = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
                Undo.RegisterCreatedObjectUndo(eventSystem.gameObject, "Create Main Menu");
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
        }
    }
}
