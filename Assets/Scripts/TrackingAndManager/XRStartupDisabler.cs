using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

// 08.08.2025 begin - Disable locomotion, teleportation, and ray-based distance grab on scene load
[DefaultExecutionOrder(-1000)]
public class XRStartupDisabler : MonoBehaviour
{
    [Header("What to disable on scene load")]
    [SerializeField] private bool disableLocomotionProviders = true;
    [SerializeField] private bool disableTeleportation = true;
    [SerializeField] private bool disableRayInteraction = true; // disables XRRayInteractor + line visuals (the arrow)
    [SerializeField] private bool disableLocomotionActions = true; // best-effort: disables Move/Turn/Teleport actions

    [Header("Targeting (optional)")]
    [SerializeField] private bool limitToSpecificControllers = false; // when true, only affects the two specified controller roots
    [SerializeField] private GameObject leftControllerRoot;  // assign the left hand controller GameObject (root that has XRRayInteractor / visuals)
    [SerializeField] private GameObject rightControllerRoot; // assign the right hand controller GameObject

    void Awake()
    {
        // Step: Disable continuous/snap movement & turn providers
        if (disableLocomotionProviders)
        {
            DisableComponentType<ActionBasedContinuousMoveProvider>();
            DisableComponentType<ActionBasedContinuousTurnProvider>();
            DisableComponentType<DeviceBasedContinuousMoveProvider>();
            DisableComponentType<DeviceBasedContinuousTurnProvider>();
            DisableComponentType<ActionBasedSnapTurnProvider>();
            DisableAllLocomotionProviders();
        }

        // Step: Disable teleportation systems (provider + interactables)
        if (disableTeleportation)
        {
            // 08.08.2025 fix: use root XR Interaction Toolkit namespace for Teleportation types
            DisableComponentType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>();
            DisableComponentType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>();
            DisableComponentType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor>();
        }

        // Step: Disable ray-based distance grabbing and hide line visuals
        if (disableRayInteraction)
        {
            if (limitToSpecificControllers)
            {
                // Disable by concrete types when available
                DisableInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(leftControllerRoot);
                DisableInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(rightControllerRoot);
                DisableInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>(leftControllerRoot);
                DisableInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>(rightControllerRoot);

                // Fallback: disable by type name to be robust across XRITK versions/namespaces
                DisableByTypeNameInChildren(leftControllerRoot, "XRRayInteractor", alsoBlockSelect: true);
                DisableByTypeNameInChildren(rightControllerRoot, "XRRayInteractor", alsoBlockSelect: true);
                DisableByTypeNameInChildren(leftControllerRoot, "XRInteractorLineVisual");
                DisableByTypeNameInChildren(rightControllerRoot, "XRInteractorLineVisual");
                DisableByTypeNameInChildren(leftControllerRoot, "XRInteractorReticleVisual");
                DisableByTypeNameInChildren(rightControllerRoot, "XRInteractorReticleVisual");
            }
            else
            {
                foreach (var ray in FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(true))
                    ray.enabled = false;
                foreach (var line in FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>(true))
                    line.enabled = false;

                // Fallback by type name
                DisableByTypeNameGlobal("XRRayInteractor", alsoBlockSelect: true);
                DisableByTypeNameGlobal("XRInteractorLineVisual");
                DisableByTypeNameGlobal("XRInteractorReticleVisual");
            }
        }

        // Step: Disable locomotion by turning off provider components instead of touching action properties (version-safe)
        if (disableLocomotionActions)
        {
            if (limitToSpecificControllers)
            {
                DisableInChildren<ActionBasedContinuousMoveProvider>(leftControllerRoot);
                DisableInChildren<ActionBasedContinuousMoveProvider>(rightControllerRoot);
                DisableInChildren<ActionBasedContinuousTurnProvider>(leftControllerRoot);
                DisableInChildren<ActionBasedContinuousTurnProvider>(rightControllerRoot);
                DisableInChildren<ActionBasedSnapTurnProvider>(leftControllerRoot);
                DisableInChildren<ActionBasedSnapTurnProvider>(rightControllerRoot);
            }
            else
            {
                foreach (var p in FindObjectsOfType<ActionBasedContinuousMoveProvider>(true))
                    p.enabled = false;
                foreach (var p in FindObjectsOfType<ActionBasedContinuousTurnProvider>(true))
                    p.enabled = false;
                foreach (var p in FindObjectsOfType<ActionBasedSnapTurnProvider>(true))
                    p.enabled = false;
            }
        }
    }

    private static void DisableAllLocomotionProviders()
    {
        // Step: Catch any custom locomotion providers derived from LocomotionProvider
        // 08.08.2025 fix: use root namespace for LocomotionProvider
        foreach (var prov in FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>(true))
        {
            prov.enabled = false;
        }
    }

    private static void DisableComponentType<T>() where T : Behaviour
    {
        var components = FindObjectsOfType<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            components[i].enabled = false;
        }
    }

    private static void DisableInChildren<T>(GameObject root) where T : Behaviour
    {
        if (root == null) return;
        var components = root.GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            components[i].enabled = false;
        }
    }

    private static bool NameMatches(string source, string token)
    {
        return source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void DisableByTypeNameInChildren(GameObject root, string typeName, bool alsoBlockSelect = false)
    {
        if (root == null) return;
        var components = root.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            var c = components[i];
            if (c == null) continue;
            if (!string.Equals(c.GetType().Name, typeName, StringComparison.Ordinal)) continue;
            if (c is Behaviour b)
            {
                b.enabled = false;
            }
            if (alsoBlockSelect)
            {
                TrySetBoolProperty(c, "allowHover", false);
                TrySetBoolProperty(c, "allowSelect", false);
                TrySetFloatProperty(c, "maxRaycastDistance", 0f);
                TrySetBoolProperty(c, "enableUIInteraction", false);
            }
        }
    }

    private static void DisableByTypeNameGlobal(string typeName, bool alsoBlockSelect = false)
    {
        var components = GameObject.FindObjectsOfType<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            var c = components[i];
            if (c == null) continue;
            if (!string.Equals(c.GetType().Name, typeName, StringComparison.Ordinal)) continue;
            if (c is Behaviour b)
            {
                b.enabled = false;
            }
            if (alsoBlockSelect)
            {
                TrySetBoolProperty(c, "allowHover", false);
                TrySetBoolProperty(c, "allowSelect", false);
                TrySetFloatProperty(c, "maxRaycastDistance", 0f);
                TrySetBoolProperty(c, "enableUIInteraction", false);
            }
        }
    }

    private static void TrySetBoolProperty(object target, string propertyName, bool value)
    {
        var t = target.GetType();
        var prop = t.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null && prop.CanWrite && prop.PropertyType == typeof(bool))
        {
            try { prop.SetValue(target, value); } catch { }
        }
    }

    private static void TrySetFloatProperty(object target, string propertyName, float value)
    {
        var t = target.GetType();
        var prop = t.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null && prop.CanWrite && prop.PropertyType == typeof(float))
        {
            try { prop.SetValue(target, value); } catch { }
        }
    }

    
}
// 08.08.2025 end


