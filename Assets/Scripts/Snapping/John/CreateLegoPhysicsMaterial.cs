using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CreateLegoPhysicsMaterial : MonoBehaviour
{
    [Header("Physics Material Settings")]
    [Tooltip("Static friction coefficient. Higher values (0.8-1.0) make bricks stick better to surfaces.")]
    public float staticFriction = 0.9f;
    
    [Tooltip("Dynamic friction coefficient. Should be slightly lower than static friction.")]
    public float dynamicFriction = 0.7f;
    
    [Tooltip("Bounciness. Keep at 0 for LEGO bricks.")]
    public float bounciness = 0.0f;
    
    [Tooltip("Friction combine mode. Use Average for most cases.")]
    public PhysicsMaterialCombine frictionCombine = PhysicsMaterialCombine.Average;
    
    [Tooltip("Bounce combine mode. Use Average for most cases.")]
    public PhysicsMaterialCombine bounceCombine = PhysicsMaterialCombine.Average;

#if UNITY_EDITOR
    [ContextMenu("Create LEGO Physics Material")]
    public void CreatePhysicsMaterial()
    {
        // Create the physics material
        PhysicsMaterial material = new PhysicsMaterial("LegoBrickMaterial");
        
        // Set the properties
        material.staticFriction = staticFriction;
        material.dynamicFriction = dynamicFriction;
        material.bounciness = bounciness;
        material.frictionCombine = frictionCombine;
        material.bounceCombine = bounceCombine;
        
        // Save the asset
        string path = "Assets/Physics Materials/LegoBrickMaterial.asset";
        
        // Create directory if it doesn't exist
        string directory = System.IO.Path.GetDirectoryName(path);
        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }
        
        AssetDatabase.CreateAsset(material, path);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"Created LEGO Physics Material at: {path}");
        Debug.Log($"Static Friction: {staticFriction}, Dynamic Friction: {dynamicFriction}, Bounciness: {bounciness}");
        
        // Select the created asset
        Selection.activeObject = material;
        EditorGUIUtility.PingObject(material);
    }
#endif

    [Header("Instructions")]
    [TextArea(5, 10)]
    public string instructions = @"To create a physics material for your LEGO bricks:

1. Select this GameObject in the scene
2. Right-click and choose 'Create LEGO Physics Material'
3. The material will be created in Assets/Physics Materials/
4. Apply the material to your BrickBehavior components by dragging it to the 'Brick Physics Material' field

Recommended settings:
- Static Friction: 0.8-1.0
- Dynamic Friction: 0.6-0.8  
- Bounciness: 0.0
- Combine Modes: Average";
} 