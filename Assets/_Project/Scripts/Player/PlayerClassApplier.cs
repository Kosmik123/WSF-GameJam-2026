using System;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Skills;

namespace WSFGameJam2026.Player
{
    /// <summary>
    /// Applies a <see cref="PlayerClass"/> to the player (port of the LD57 PlayerClassApplier, without reflection):
    /// melee or ranged attack, maximum health, skill pool and starting skills, character model.
    /// Runs before the other components, so they start with the class already set.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(PlayerCombat), typeof(Health))]
    public class PlayerClassApplier : MonoBehaviour
    {
        /// <summary>The character model was replaced. The argument is its Animator (null when the class has no model).</summary>
        public event Action<Animator> OnModelChanged;

        [SerializeField]
        private PlayerClass playerClass;

        [Tooltip("Parent of the character model, at the feet of the player.")]
        [SerializeField]
        private Transform modelRoot;

        [Tooltip("Objects shown only when the class has no model (the placeholder capsule and its nose).")]
        [SerializeField]
        private GameObject[] placeholders = new GameObject[0];

        private GameObject model;

        public PlayerClass Current { get; private set; }
        public Animator ModelAnimator { get; private set; }

        private void Reset()
        {
            modelRoot = transform.Find("Model");
            var capsule = transform.Find("Capsule");
            var nose = transform.Find("Cube");
            if (capsule && nose)
                placeholders = new[] { capsule.gameObject, nose.gameObject };
        }

        private void Awake()
        {
            Apply(playerClass);
        }

        /// <summary>Applies the class. Resets the skills and fills the health. Safe to call during the game (class choice).</summary>
        public void Apply(PlayerClass newClass)
        {
            if (newClass == null)
                return;

            Current = newClass;
            GetComponent<PlayerCombat>().MeleeEnabled = newClass.IsRanged == false;

            if (TryGetComponent<RangedAttack>(out var rangedAttack))
            {
                rangedAttack.Configure(newClass.RangedDamage, newClass.RangedCooldown);
                rangedAttack.enabled = newClass.IsRanged;
            }

            if (TryGetComponent<SkillSlots>(out var skillSlots))
                skillSlots.SetPool(newClass.SkillPool, newClass.StartingSkills);

            GetComponent<Health>().SetMaxHealth(newClass.MaxHealth, true);
            ApplyModel(newClass);
        }

        private void ApplyModel(PlayerClass newClass)
        {
            // Also removes the preview model placed under the model root in the editor (the hero is visible without Play).
            if (modelRoot)
            {
                // Hidden at once, because Destroy waits for the end of the frame and the flash would still find its renderers.
                for (int i = modelRoot.childCount - 1; i >= 0; i--)
                {
                    var child = modelRoot.GetChild(i).gameObject;
                    child.SetActive(false);
                    Destroy(child);
                }
            }

            if (model)
                Destroy(model);

            model = null;
            ModelAnimator = null;
            if (newClass.ModelPrefab && modelRoot)
            {
                model = Instantiate(newClass.ModelPrefab, modelRoot);
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                HideParts(newClass);
                SetMaterial(newClass.ModelMaterial);
                ModelAnimator = model.GetComponentInChildren<Animator>();
            }

            for (int i = 0; i < placeholders.Length; i++)
                if (placeholders[i])
                    placeholders[i].SetActive(model == null);

            if (TryGetComponent<BodyFlash>(out var bodyFlash))
                bodyFlash.RefreshRenderers();

            OnModelChanged?.Invoke(ModelAnimator);
        }

        private void HideParts(PlayerClass newClass)
        {
            var parts = newClass.HiddenParts;
            foreach (var part in model.GetComponentsInChildren<Transform>(true))
            {
                for (int i = 0; i < parts.Count; i++)
                    if (part.name == parts[i])
                        part.gameObject.SetActive(false);
            }
        }

        private void SetMaterial(Material material)
        {
            if (material == null)
                return;

            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = material;

                renderer.sharedMaterials = materials;
            }
        }
    }
}
