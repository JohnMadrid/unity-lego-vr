// Assets/Editor/LegoBrickGenerator.cs
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A popup window to allow the user to select a color for the generated brick.
/// </summary>
public class LegoColorSelectorWindow : EditorWindow
{
    private int width;
    private int length;
    private bool isBoard;

    public static void ShowWindow(int w, int l, bool board = false)
    {
        var window = GetWindow<LegoColorSelectorWindow>("Select Color");
        window.width = w;
        window.length = l;
        window.isBoard = board;
        window.minSize = new Vector2(250, 150);
        window.maxSize = new Vector2(250, 150);
    }

    void OnGUI()
    {
        // Special case for the baseplate which has a fixed color
        if (isBoard)
        {
            EditorGUILayout.LabelField($"Creating a {width}x{length} baseplate.", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Baseplates are always created in Gray.", MessageType.Info);
            if (GUILayout.Button("Create"))
            {
                LegoBrickGenerator.CreateBoard(width, length);
                this.Close();
            }
            return;
        }

        EditorGUILayout.LabelField($"Creating a {width}x{length} brick.", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Please select a color:");

        GUILayout.Space(10);

        var colors = LegoBrickGenerator.GetLegoColors();
        foreach (var color in colors)
        {
            var style = new GUIStyle(GUI.skin.button);
            // Make text color readable on dark backgrounds
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;

            GUI.backgroundColor = color.Key;
            
            if (GUILayout.Button(color.Value, style))
            {
                LegoBrickGenerator.CreateBrick(this.width, this.length, color.Key);
                this.Close();
            }
        }
        
        GUI.backgroundColor = Color.white; // Reset background color for other UI elements
    }
}
 
public static class LegoBrickGenerator
{
    const float UNIT = 0.4f;
    const float BRICK_HEIGHT = 0.8f * UNIT;
    const float PLATE_HEIGHT = BRICK_HEIGHT / 3f;
    const float STUD_HEIGHT = 0.2f * UNIT;
    const float STUD_RADIUS = 0.225f * UNIT;
    const int Fidelity = 8;
    const int FACE_GRID_RESOLUTION = 4;
    private static readonly Vector3 BRICK_SCALE = new Vector3(0.11f, 0.11f, 0.11f);
    
    // Counter for unique names
    private static Dictionary<string, int> brickCounters = new Dictionary<string, int>();

    /// <summary>
    /// Defines the standard set of LEGO colors used for brick generation.
    /// </summary>
    public static Dictionary<Color, string> GetLegoColors()
    {
        var colorDict = new Dictionary<Color, string>();
        Color color;

        ColorUtility.TryParseHtmlString("#0072B2", out color);
        colorDict.Add(color, "Blue");

        ColorUtility.TryParseHtmlString("#D55E00", out color);
        colorDict.Add(color, "Orange");
        
        ColorUtility.TryParseHtmlString("#CC79A7", out color);
        colorDict.Add(color, "Pink");
        
        ColorUtility.TryParseHtmlString("#009E73", out color);
        colorDict.Add(color, "Green");

        return colorDict;
    }
 
    [MenuItem("GameObject/Create Lego Bricks/Generate 1×1 Brick", false, 10)]
    public static void Create1x1() => LegoColorSelectorWindow.ShowWindow(1, 1);
 
    [MenuItem("GameObject/Create Lego Bricks/Generate 1×2 Brick", false, 11)]
    public static void Create1x2() => LegoColorSelectorWindow.ShowWindow(1, 2);
 
    [MenuItem("GameObject/Create Lego Bricks/Generate 2×2 Brick", false, 12)]
    public static void Create2x2() => LegoColorSelectorWindow.ShowWindow(2, 2);
 
    [MenuItem("GameObject/Create Lego Bricks/Generate 3×2 Brick", false, 13)]
    public static void Create3x2() => LegoColorSelectorWindow.ShowWindow(3, 2);
 
    [MenuItem("GameObject/Create Lego Bricks/Generate 4×2 Brick", false, 14)]
    public static void Create4x2() => LegoColorSelectorWindow.ShowWindow(4, 2);

    [MenuItem("GameObject/Create Lego Bricks/Generate 1×3 Brick", false, 15)]
    public static void Create1x3() => LegoColorSelectorWindow.ShowWindow(1, 3);
 
    [MenuItem("GameObject/Create Lego Bricks/Generate 10x10 Baseplate", false, 100)]
    public static void Create10x10Baseplate() => LegoColorSelectorWindow.ShowWindow(10, 10, true);
 
    public static void CreateBrick(int width, int length, Color brickColor)
    {
        string colorName = GetColorName(brickColor);

        // --- Unique name logic ---
        string baseName = $"Lego_{width}x{length}_{colorName}";
        if (!brickCounters.ContainsKey(baseName))
        {
            brickCounters[baseName] = 0;
        }
        brickCounters[baseName]++;
        string finalName = $"{baseName}_{brickCounters[baseName]}";
        
        // 1) Parent container
        var brick = new GameObject(finalName);
        brick.transform.localScale = BRICK_SCALE;
        brick.transform.position = new Vector3(1f, 1f, 0f);
        brick.transform.rotation = Quaternion.Euler(90, 0, 0);
        brick.tag = "Brick";
        Undo.RegisterCreatedObjectUndo(brick, "Create Lego Brick");
 
        // 2) Create main body and studs as a single custom mesh
        var body = new GameObject("Body");
        body.transform.SetParent(brick.transform, false);
        
        // Apply custom material with random color
        var meshRenderer = body.AddComponent<MeshRenderer>();
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = brickColor;
        material.SetFloat("_Metallic", 0.0f); // Plastic is a non-metal
        material.SetFloat("_Smoothness", 0.1f); // High smoothness for a shiny finish
        meshRenderer.sharedMaterial = material;

        // Generate and combine meshes
        var meshFilter = body.AddComponent<MeshFilter>();
        meshFilter.mesh = CreateBrickMesh(width, length);

        // Position body so that the brick's transform is at the center of the mesh
        body.transform.localPosition = Vector3.zero;
 
        // 3) Add one BoxCollider to the container matching the brick dimensions
        var boxCol = brick.AddComponent<BoxCollider>();
        boxCol.center = Vector3.zero;
        boxCol.size   = new Vector3(width * UNIT, length * UNIT, BRICK_HEIGHT);
 
        // 4) Grab the material to reuse on studs/tubes
        var mat = body.GetComponent<Renderer>().sharedMaterial;

        // 5. Add colliders for the top studs
        int topStudIndex = 0;
        for (int x = 0; x < width; x++)
        for (int y = 0; y < length; y++)
        {
            float posX = (x - (width - 1) / 2f) * UNIT;
            float posY = (y - (length - 1) / 2f) * UNIT;

            var studCollider = new GameObject($"stud_top_{topStudIndex++}");
            studCollider.transform.SetParent(brick.transform, false);
            studCollider.transform.localPosition = new Vector3(posX, posY, -BRICK_HEIGHT / 2f);
            
            var sph = studCollider.AddComponent<SphereCollider>();
            sph.radius = STUD_RADIUS;
            // Attach Stud component and set type to Top
            var studComp = studCollider.AddComponent<Stud>();
            studComp.Type = Stud.StudType.Top;
        }

        // 6. Add colliders for the female studs (holes)
        int btmStudIndex = 0;
        for (int x = 0; x < width; x++)
        for (int y = 0; y < length; y++)
        {
            float posX = (x - (width - 1) / 2f) * UNIT;
            float posY = (y - (length - 1) / 2f) * UNIT;

            var studCollider = new GameObject($"stud_btm_{btmStudIndex++}");
            studCollider.transform.SetParent(brick.transform, false);
            studCollider.transform.localPosition = new Vector3(posX, posY, BRICK_HEIGHT / 2f);
            
            var sph = studCollider.AddComponent<SphereCollider>();
            sph.radius = STUD_RADIUS * 1.05f;
            // Attach Stud component and set type to Bottom
            var studComp = studCollider.AddComponent<Stud>();
            studComp.Type = Stud.StudType.Bottom;
        }

        // 7) Add Rigidbody component
        var rigidbody = brick.AddComponent<Rigidbody>();
        rigidbody.mass = 1f;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rigidbody.useGravity = true;
        rigidbody.isKinematic = false;

        // 8) Add BrickReset script
        brick.AddComponent<BrickReset>();

        // 9) Add XRGrabInteractable component
        var xrGrabInteractable = brick.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        xrGrabInteractable.attachTransform = body.transform;
        xrGrabInteractable.interactionLayers = 1;
        xrGrabInteractable.distanceCalculationMode = (UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.DistanceCalculationMode)1;
        xrGrabInteractable.selectMode = 0;
        xrGrabInteractable.focusMode = (UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableFocusMode)1;
        xrGrabInteractable.allowGazeInteraction = false;
        xrGrabInteractable.allowGazeSelect = false;
        xrGrabInteractable.gazeTimeToSelect = 0.5f;
        xrGrabInteractable.timeToAutoDeselectGaze = 3f;
        xrGrabInteractable.useDynamicAttach = true;
        xrGrabInteractable.matchAttachPosition = true;
        xrGrabInteractable.matchAttachRotation = true;
        xrGrabInteractable.snapToColliderVolume = true;
        xrGrabInteractable.reinitializeDynamicAttachEverySingleGrab = true;
        xrGrabInteractable.attachEaseInTime = 0.15f;
        xrGrabInteractable.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable.MovementType.Instantaneous;
        xrGrabInteractable.velocityDamping = 1f;
        xrGrabInteractable.velocityScale = 1f;
        xrGrabInteractable.angularVelocityDamping = 1f;
        xrGrabInteractable.angularVelocityScale = 1f;
        xrGrabInteractable.trackPosition = true;
        xrGrabInteractable.smoothPosition = true;
        xrGrabInteractable.smoothPositionAmount = 5f;
        xrGrabInteractable.tightenPosition = 0.1f;
        xrGrabInteractable.trackRotation = true;
        xrGrabInteractable.smoothRotation = true;
        xrGrabInteractable.smoothRotationAmount = 5f;
        xrGrabInteractable.tightenRotation = 0.1f;
        xrGrabInteractable.trackScale = true;
        xrGrabInteractable.smoothScale = true;
        xrGrabInteractable.smoothScaleAmount = 5f;
        xrGrabInteractable.tightenScale = 0.1f;
        xrGrabInteractable.throwOnDetach = false;
        xrGrabInteractable.throwSmoothingDuration = 0.25f;
        xrGrabInteractable.throwVelocityScale = 1.5f;
        xrGrabInteractable.throwAngularVelocityScale = 1f;
        xrGrabInteractable.forceGravityOnDetach = false;
        xrGrabInteractable.retainTransformParent = true;
        xrGrabInteractable.addDefaultGrabTransformers = true;
        xrGrabInteractable.farAttachMode = 0;

        // 10) Add BrickBehavior script
        brick.AddComponent<BrickBehavior>();

        // 11) Rotate the brick so that studs face up (along the Y-axis).
 
        // 12) Select new brick
        Selection.activeGameObject = brick;
    }
    
    public static void CreateBoard(int width, int length)
    {
        // 1) Parent container
        var board = new GameObject($"Lego_Board_{width}x{length}");
        board.transform.localScale = BRICK_SCALE;
        board.transform.position = new Vector3(1f, 1f, 0f);
        board.transform.rotation = Quaternion.Euler(90, 0, 0);
        board.tag = "Board";
        Undo.RegisterCreatedObjectUndo(board, "Create Lego Board");

        // 2) Create main body and studs as a single custom mesh
        var body = new GameObject("Body");
        body.transform.SetParent(board.transform, false);
        
        var meshRenderer = body.AddComponent<MeshRenderer>();
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = new Color(0.5f, 0.5f, 0.5f); // Default gray for baseplates
        material.SetFloat("_Metallic", 0.0f);
        material.SetFloat("_Smoothness", 0.1f);
        meshRenderer.sharedMaterial = material;

        var meshFilter = body.AddComponent<MeshFilter>();
        meshFilter.mesh = CreateBoardMesh(width, length);
        body.transform.localPosition = Vector3.zero;
 
        // 3) Add one BoxCollider to the container
        var boxCol = board.AddComponent<BoxCollider>();
        boxCol.center = Vector3.zero;
        boxCol.size   = new Vector3(width * UNIT, length * UNIT, PLATE_HEIGHT);
 
        // 4. Add colliders for the top studs
        int topStudIndex = 0;
        for (int x = 0; x < width; x++)
        for (int y = 0; y < length; y++)
        {
            float posX = (x - (width - 1) / 2f) * UNIT;
            float posY = (y - (length - 1) / 2f) * UNIT;

            var studCollider = new GameObject($"stud_top_{topStudIndex++}");
            studCollider.transform.SetParent(board.transform, false);
            studCollider.transform.localPosition = new Vector3(posX, posY, -PLATE_HEIGHT / 2f);
            
            var sph = studCollider.AddComponent<SphereCollider>();
            sph.radius = STUD_RADIUS * 1.33f; // 33% larger than brick studs
            var studComp = studCollider.AddComponent<Stud>();
            studComp.Type = Stud.StudType.Top;
        }

        // 5) Add Rigidbody component
        var rigidbody = board.AddComponent<Rigidbody>();
        rigidbody.mass = 5f; // Heavier mass for a large baseplate
        rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rigidbody.useGravity = true;

        // 6) Add BrickBehavior script
        board.AddComponent<BrickBehavior>();

        // 7) Select new board
        Selection.activeGameObject = board;
    }
    
    private static string GetColorName(Color color)
    {
        var colors = GetLegoColors();
        // Because of float precision, direct dictionary lookup might fail.
        // It's safer to iterate and compare with a small tolerance.
        foreach (var kvp in colors)
        {
            if (Mathf.Approximately(kvp.Key.r, color.r) &&
                Mathf.Approximately(kvp.Key.g, color.g) &&
                Mathf.Approximately(kvp.Key.b, color.b))
            {
                return kvp.Value;
            }
        }
        
        if (color == new Color(0.5f, 0.5f, 0.5f)) return "Gray";
        
        return "Unknown";
    }
    
    private static Mesh CreateBrickMesh(int width, int length)
    {
        var combine = new List<CombineInstance>();

        float brickHeight = BRICK_HEIGHT;
        float halfHeight = brickHeight / 2f;

        // Part 1: Side Walls
        var sideWallsMesh = CreateSideWallsMesh(width, length, brickHeight);
        combine.Add(new CombineInstance { mesh = sideWallsMesh, transform = Matrix4x4.identity });

        // Part 2: Top Face (with holes for studs), created tile by tile
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < length; y++)
            {
                var tileCenter = new Vector2((x - (width - 1) / 2f) * UNIT, (y - (length - 1) / 2f) * UNIT);
                var tileTopMesh = CreateFaceWithHoles(1, 1, new[] { Vector2.zero }, STUD_RADIUS, true); // Inverted
                var transform = Matrix4x4.TRS(
                    new Vector3(tileCenter.x, tileCenter.y, -halfHeight),
                    Quaternion.identity,
                    Vector3.one);
                combine.Add(new CombineInstance { mesh = tileTopMesh, transform = transform });
            }
        }

        // Part 3: Bottom Face (with holes for female studs), created tile by tile
        float bottomHoleRadius = STUD_RADIUS * 1.05f;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < length; y++)
            {
                var tileCenter = new Vector2((x - (width - 1) / 2f) * UNIT, (y - (length - 1) / 2f) * UNIT);
                var tileBottomMesh = CreateFaceWithHoles(1, 1, new[] { Vector2.zero }, bottomHoleRadius, false); // Not inverted
                var transform = Matrix4x4.TRS(
                    new Vector3(tileCenter.x, tileCenter.y, halfHeight),
                    Quaternion.identity,
                    Vector3.one);
                combine.Add(new CombineInstance { mesh = tileBottomMesh, transform = transform });
            }
        }

        var studPositions = GetStudPositions(width, length);

        // Part 4: Top Studs
        foreach (var pos in studPositions)
        {
            var studMesh = CreateCylinderMesh(STUD_RADIUS, STUD_HEIGHT, Fidelity, true, false); // Has caps
            var studTransform = Matrix4x4.Translate(new Vector3(pos.x, pos.y, -(halfHeight + STUD_HEIGHT / 2)));
            combine.Add(new CombineInstance { mesh = studMesh, transform = studTransform });
        }
        
        // Part 5: Inner Walls for Holes
        foreach (var pos in studPositions)
        {
            float holeDepth = STUD_HEIGHT * 1.05f;
            var holeWallMesh = CreateCylinderMesh(STUD_RADIUS * 1.05f, holeDepth, Fidelity, false, true); // No caps, inverted
            var holeWallTransform = Matrix4x4.Translate(new Vector3(pos.x, pos.y, halfHeight - holeDepth / 2f));
            combine.Add(new CombineInstance { mesh = holeWallMesh, transform = holeWallTransform });
        }
        
        // Part 6: Bottom caps for holes
        foreach (var pos in studPositions)
        {
            float holeDepth = STUD_HEIGHT * 1.05f;
            // We create the cap by generating a cylinder with caps but almost zero height.
            var holeCapMesh = CreateCylinderMesh(STUD_RADIUS * 1.05f, 0.001f, Fidelity, true, false);
            // Position the cap at the end of the hole's depth.
            var holeCapTransform = Matrix4x4.Translate(new Vector3(pos.x, pos.y, halfHeight - holeDepth));
            combine.Add(new CombineInstance { mesh = holeCapMesh, transform = holeCapTransform });
        }

        // Final Combination
        var finalMesh = new Mesh();
        finalMesh.CombineMeshes(combine.ToArray(), true, true);
        return finalMesh;
    }

    private static Vector2[] GetStudPositions(int width, int length)
    {
        var positions = new List<Vector2>();
        for (int x = 0; x < width; x++)
        for (int y = 0; y < length; y++)
        {
            float posX = (x - (width - 1) / 2f) * UNIT;
            float posY = (y - (length - 1) / 2f) * UNIT;
            positions.Add(new Vector2(posX, posY));
        }
        return positions.ToArray();
    }

    private static Mesh CreateBoardMesh(int width, int length)
    {
        var combine = new List<CombineInstance>();
        float halfHeight = PLATE_HEIGHT / 2f;

        // Part 1: Baseplate Box
        var boxMesh = CreateBoxMesh(width * UNIT, length * UNIT, PLATE_HEIGHT);
        combine.Add(new CombineInstance { mesh = boxMesh, transform = Matrix4x4.identity });

        // Part 2: Top Studs
        var studPositions = GetStudPositions(width, length);
        foreach (var pos in studPositions)
        {
            var studMesh = CreateCylinderMesh(STUD_RADIUS, STUD_HEIGHT, Fidelity, true, false);
            var studTransform = Matrix4x4.Translate(new Vector3(pos.x, pos.y, -(halfHeight + STUD_HEIGHT / 2f)));
            combine.Add(new CombineInstance { mesh = studMesh, transform = studTransform });
        }

        var finalMesh = new Mesh();
        finalMesh.CombineMeshes(combine.ToArray(), true, true);
        return finalMesh;
    }
    
    private static Mesh CreateBoxMesh(float width, float height, float depth)
    {
        var mesh = new Mesh();
        float halfW = width / 2f;
        float halfH = height / 2f;
        float halfD = depth / 2f;

        var vertices = new Vector3[]
        {
            // Front (+Z)
            new Vector3(-halfW, -halfH, halfD), new Vector3(halfW, -halfH, halfD), new Vector3(halfW, halfH, halfD), new Vector3(-halfW, halfH, halfD),
            // Back (-Z)
            new Vector3(-halfW, halfH, -halfD), new Vector3(halfW, halfH, -halfD), new Vector3(halfW, -halfH, -halfD), new Vector3(-halfW, -halfH, -halfD),
            // Top (+Y)
            new Vector3(-halfW, halfH, halfD), new Vector3(halfW, halfH, halfD), new Vector3(halfW, halfH, -halfD), new Vector3(-halfW, halfH, -halfD),
            // Bottom (-Y)
            new Vector3(-halfW, -halfH, -halfD), new Vector3(halfW, -halfH, -halfD), new Vector3(halfW, -halfH, halfD), new Vector3(-halfW, -halfH, halfD),
            // Right (+X)
            new Vector3(halfW, -halfH, halfD), new Vector3(halfW, -halfH, -halfD), new Vector3(halfW, halfH, -halfD), new Vector3(halfW, halfH, halfD),
            // Left (-X)
            new Vector3(-halfW, -halfH, -halfD), new Vector3(-halfW, -halfH, halfD), new Vector3(-halfW, halfH, halfD), new Vector3(-halfW, halfH, -halfD)
        };
        
        var triangles = new int[]
        {
            0, 1, 2, 0, 2, 3, // Front
            4, 5, 6, 4, 6, 7, // Back
            8, 9, 10, 8, 10, 11, // Top
            12, 13, 14, 12, 14, 15, // Bottom
            16, 17, 18, 16, 18, 19, // Right
            20, 21, 22, 20, 22, 23  // Left
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Mesh CreateSideWallsMesh(int width, int length, float height)
    {
        var mesh = new Mesh();
        float halfW = width * UNIT / 2f;
        float halfL = length * UNIT / 2f;
        float halfH = height / 2f;

        var vertices = new Vector3[]
        {
            // Front (along -Y)
            new Vector3(-halfW, -halfL, halfH), new Vector3(halfW, -halfL, halfH), new Vector3(halfW, -halfL, -halfH), new Vector3(-halfW, -halfL, -halfH),
            // Back (along +Y)
            new Vector3(halfW, halfL, halfH), new Vector3(-halfW, halfL, halfH), new Vector3(-halfW, halfL, -halfH), new Vector3(halfW, halfL, -halfH),
            // Left (along -X)
            new Vector3(-halfW, halfL, halfH), new Vector3(-halfW, -halfL, halfH), new Vector3(-halfW, -halfL, -halfH), new Vector3(-halfW, halfL, -halfH),
            // Right (along +X)
            new Vector3(halfW, -halfL, halfH), new Vector3(halfW, halfL, halfH), new Vector3(halfW, halfL, -halfH), new Vector3(halfW, -halfL, -halfH),
        };

        var triangles = new int[]
        {
            0, 2, 1, 0, 3, 2,
            4, 6, 5, 4, 7, 6,
            8, 10, 9, 8, 11, 10,
            12, 14, 13, 12, 15, 14
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Mesh CreateFaceWithHoles(int width, int length, Vector2[] holePositions, float holeRadius, bool invert)
    {
        float halfW = width * UNIT / 2f;
        float halfL = length * UNIT / 2f;

        var boundary = new Vector2[]
        {
            new Vector2(-halfW, -halfL),
            new Vector2(halfW, -halfL),
            new Vector2(halfW, halfL),
            new Vector2(-halfW, halfL)
        };

        var holes = new Vector2[holePositions.Length][];
        for (int i = 0; i < holePositions.Length; i++)
        {
            var holePath = new Vector2[Fidelity];
            for (int j = 0; j < Fidelity; j++)
            {
                float angle = j * 2f * Mathf.PI / Fidelity;
                float x = holePositions[i].x + Mathf.Cos(angle) * holeRadius;
                float y = holePositions[i].y + Mathf.Sin(angle) * holeRadius;
                holePath[j] = new Vector2(x, y);
            }
            holes[i] = holePath;
        }

        var triangulator = new Triangulator(boundary, holes);
        var indices = triangulator.Triangulate();

        var vertices = triangulator.Points.Select(p => new Vector3(p.x, p.y, 0)).ToArray();
        
        if (invert)
        {
            for (int i = 0; i < indices.Length; i += 3)
            {
                int temp = indices[i];
                indices[i] = indices[i + 2];
                indices[i + 2] = temp;
            }
        }

        var mesh = new Mesh
        {
            vertices = vertices,
            triangles = indices
        };
        
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
 
    private static Mesh CreateCylinderMesh(float radius, float height, int segments = 16, bool addCaps = true, bool invertNormals = false)
    {
        var mesh = new Mesh();
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        float halfHeight = height / 2f;
        int vertIndex = 0;

        // Wall vertices
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            vertices.Add(new Vector3(x, y, halfHeight)); // Bottom
            vertices.Add(new Vector3(x, y, -halfHeight));  // Top
        }

        // Wall triangles
        for (int i = 0; i < segments; i++)
        {
            int currentBase = i * 2;
            int nextBase = currentBase + 2;

            if (invertNormals)
            {
                triangles.Add(currentBase);
                triangles.Add(nextBase);
                triangles.Add(currentBase + 1);

                triangles.Add(nextBase);
                triangles.Add(nextBase + 1);
                triangles.Add(currentBase + 1);
            }
            else
            {
                triangles.Add(currentBase);
                triangles.Add(currentBase + 1);
                triangles.Add(nextBase);

                triangles.Add(nextBase);
                triangles.Add(currentBase + 1);
                triangles.Add(nextBase + 1);
            }
        }
        vertIndex += (segments + 1) * 2;

        if (addCaps)
        {
            // Top cap - faces +Y, so should be clockwise (CW)
            int topCenterIndex = vertIndex++;
            vertices.Add(new Vector3(0, 0, -halfHeight));
            for (int i = 0; i < segments; i++)
            {
                int currentTop = i * 2 + 1;
                int nextTop = ((i + 1) % segments) * 2 + 1;
                if(invertNormals)
                    triangles.AddRange(new int[] { topCenterIndex, currentTop, nextTop }); // Inverted = CCW
                else
                    triangles.AddRange(new int[] { topCenterIndex, nextTop, currentTop }); // Standard = CW
            }

            // Bottom cap - faces -Y, so should be counter-clockwise (CCW)
            int bottomCenterIndex = vertIndex++;
            vertices.Add(new Vector3(0, 0, halfHeight));
            for (int i = 0; i < segments; i++)
            {
                int currentBottom = i * 2;
                int nextBottom = ((i + 1) % segments) * 2;
                if(invertNormals)
                    triangles.AddRange(new int[] { bottomCenterIndex, nextBottom, currentBottom }); // Inverted = CW
                else
                    triangles.AddRange(new int[] { bottomCenterIndex, currentBottom, nextBottom }); // Standard = CCW
            }
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Mesh CreateDiskMesh(float radius, int segments, bool invert)
    {
        var mesh = new Mesh();
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        // Center vertex
        vertices.Add(Vector3.zero);

        // Circle vertices
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            vertices.Add(new Vector3(x, y, 0));
        }

        // Triangles
        for (int i = 1; i <= segments; i++)
        {
            int current = i;
            int next = i + 1;
            // The center vertex is always at index 0
            if (invert)
                triangles.AddRange(new int[] { 0, next, current });
            else
                triangles.AddRange(new int[] { 0, current, next });
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }
}

// Based on https://github.com/derFruehling/unity-triangulator
//
// This is a port of the Javascript implementation of the earcut algorithm
// by Mapbox (https://github.com/mapbox/earcut)
public class Triangulator
{
    private class Node
    {
        public readonly int i;
        public readonly double x;
        public readonly double y;

        public Node(int i, double x, double y)
        {
            this.i = i;
            this.x = x;
            this.y = y;
        }

        public Node prev;
        public Node next;
        
        public double z;
        
        public Node prevZ;
        public Node nextZ;
        
        public bool steiner;
    }
    
    private readonly List<int> _indices = new List<int>();
    private readonly List<Vector2> m_points = new List<Vector2>();

    public Vector2[] Points => m_points.ToArray();

    public Triangulator(Vector2[] boundary, Vector2[][] holes)
    {
        var vertices = new List<double>();
        var holeIndices = new List<int>();

        foreach (var point in boundary)
        {
            m_points.Add(point);
            vertices.Add(point.x);
            vertices.Add(point.y);
        }
        
        foreach (var hole in holes)
        {
            holeIndices.Add(vertices.Count / 2);
            foreach (var point in hole)
            {
                m_points.Add(point);
                vertices.Add(point.x);
                vertices.Add(point.y);
            }
        }

        Earcut(vertices, holeIndices, 2);
    }
    
    public int[] Triangulate() {
        return _indices.ToArray();
    }
    
    private void Earcut(List<double> vertices, List<int> holeIndices, int dim) {
        var hasHoles = holeIndices.Count > 0;
        var outerLen = hasHoles ? holeIndices[0] * dim : vertices.Count;
        var outerNode = LinkedList(vertices, 0, outerLen, dim, true);
        var queue = new List<Node>();

        if (outerNode == null || outerNode.next == outerNode.prev) return;

        double minX = 0, minY = 0, maxX, maxY, x, y, invSize = 0;

        if (hasHoles) outerNode = EliminateHoles(vertices, holeIndices, outerNode, dim);
        
        if (vertices.Count > 80 * dim) {
            minX = maxX = vertices[0];
            minY = maxY = vertices[1];

            for (var i = dim; i < outerLen; i += dim) {
                x = vertices[i];
                y = vertices[i + 1];
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
            
            invSize = System.Math.Max(maxX - minX, maxY - minY);
            if (invSize == 0) invSize = 1;
        }

        EarcutLinked(outerNode, _indices, dim, minX, minY, invSize, 0);
    }
    
    private static int CompareX(Node a, Node b) {
        return System.Math.Sign(a.x - b.x);
    }
    
    private void EarcutLinked(Node ear, List<int> indices, int dim, double minX, double minY, double invSize, int pass) {
        if (ear == null) return;
        
        if (pass == 0 && invSize != 0) IndexCurve(ear, minX, minY, invSize);

        var stop = ear;

        while (ear.prev != ear.next) {
            var prev = ear.prev;
            var next = ear.next;

            if (invSize != 0 ? IsEarHashed(ear, minX, minY, invSize) : IsEar(ear)) {
                indices.Add(prev.i / dim);
                indices.Add(ear.i / dim);
                indices.Add(next.i / dim);

                prev.next = next;
                next.prev = prev;

                if (invSize != 0) {
                    RemoveNode(ear);
                }

                ear = next.next;
                stop = next.next;

                continue;
            }

            ear = next;
            
            if (ear == stop) {
                if (pass == 0) {
                    EarcutLinked(FilterPoints(ear, null), indices, dim, minX, minY, invSize, 1);
                } else if (pass == 1) {
                    ear = CureLocalIntersections(FilterPoints(ear, null), indices, dim);
                    EarcutLinked(ear, indices, dim, minX, minY, invSize, 2);
                } else if (pass == 2) {
                    SplitEarcut(FilterPoints(ear, null), indices, dim, minX, minY, invSize);
                }

                break;
            }
        }
    }
    
    private static bool IsEar(Node ear) {
        var a = ear.prev;
        var b = ear;
        var c = ear.next;

        if (Area(a, b, c) >= 0) return false;

        var p = ear.next.next;

        while (p != ear.prev) {
            if (PointInTriangle(a.x, a.y, b.x, b.y, c.x, c.y, p.x, p.y) &&
                Area(p.prev, p, p.next) >= 0) return false;
            p = p.next;
        }

        return true;
    }

    private static bool IsEarHashed(Node ear, double minX, double minY, double invSize) {
        var a = ear.prev;
        var b = ear;
        var c = ear.next;

        if (Area(a, b, c) >= 0) return false;

        var minTX = a.x < b.x ? (a.x < c.x ? a.x : c.x) : (b.x < c.x ? b.x : c.x);
        var minTY = a.y < b.y ? (a.y < c.y ? a.y : c.y) : (b.y < c.y ? b.y : c.y);
        var maxTX = a.x > b.x ? (a.x > c.x ? a.x : c.x) : (b.x > c.x ? b.x : c.x);
        var maxTY = a.y > b.y ? (a.y > c.y ? a.y : c.y) : (b.y > c.y ? b.y : c.y);

        var minZ = ZOrder(minTX, minTY, minX, minY, invSize);
        var maxZ = ZOrder(maxTX, maxTY, minX, minY, invSize);

        var p = ear.prevZ;
        var n = ear.nextZ;
        
        while (p != null && p.z >= minZ && n != null && n.z <= maxZ) {
            if (p != ear.prev && p != ear.next &&
                PointInTriangle(a.x, a.y, b.x, b.y, c.x, c.y, p.x, p.y) &&
                Area(p.prev, p, p.next) >= 0) return false;
            p = p.prevZ;

            if (n != ear.prev && n != ear.next &&
                PointInTriangle(a.x, a.y, b.x, b.y, c.x, c.y, n.x, n.y) &&
                Area(n.prev, n, n.next) >= 0) return false;
            n = n.nextZ;
        }

        return true;
    }
    
    private static Node CureLocalIntersections(Node startNode, List<int> indices, int dim) {
        var p = startNode;
        do {
            var a = p.prev;
            var b = p.next.next;

            if (!Equals(a, b) && Intersects(a, p, p.next, b) &&
                LocallyInside(a, b) && LocallyInside(b, a)) {
                indices.Add(a.i / dim);
                indices.Add(p.i / dim);
                indices.Add(b.i / dim);
                
                a.next = b;
                b.prev = a;

                var az = a.z;
                var bz = b.z;

                if (az < bz) {
                    var node = a;
                    while (node != null && node.z < bz) {
                        node.z = bz;
                        node = node.nextZ;
                    }
                } else {
                    var node = b;
                    while (node != null && node.z < az) {
                        node.z = az;
                        node = node.nextZ;
                    }
                }
                
                p = startNode = b;
            }
            p = p.next;
        } while (p != startNode);

        return FilterPoints(p, null);
    }
    
    private void SplitEarcut(Node startNode, List<int> indices, int dim, double minX, double minY, double invSize) {
        var a = startNode;
        do {
            var b = a.next.next;
            while (b != a.prev) {
                if (a.i != b.i && IsValidDiagonal(a, b)) {
                    var c = SplitPolygon(a, b);
                    var a2 = FilterPoints(a, a.next);
                    var b2 = FilterPoints(c, c.next);
                    EarcutLinked(a2, indices, dim, minX, minY, invSize, 0);
                    EarcutLinked(b2, indices, dim, minX, minY, invSize, 0);
                    return;
                }
                b = b.next;
            }
            a = a.next;
        } while (a != startNode);
    }

    private Node EliminateHoles(List<double> vertices, List<int> holeIndices, Node outerNode, int dim) {
        var queue = new List<Node>();
        int i, len, start, end;
        Node list;

        for (i = 0, len = holeIndices.Count; i < len; i++) {
            start = holeIndices[i] * dim;
            end = i < len - 1 ? holeIndices[i + 1] * dim : vertices.Count;
            list = LinkedList(vertices, start, end, dim, false);
            if (list == list.next) list.steiner = true;
            queue.Add(GetLeftmost(list));
        }

        queue.Sort(CompareX);

        for (i = 0; i < queue.Count; i++) {
            EliminateHole(queue[i], outerNode);
            outerNode = FilterPoints(outerNode, outerNode.next);
        }

        return outerNode;
    }
    
    private void EliminateHole(Node holeNode, Node outerNode) {
        var bridge = FindBridge(outerNode, holeNode);
        if (bridge != null) {
            var b = SplitPolygon(bridge, holeNode);
            FilterPoints(b, b.next);
        }
    }
    
    private static Node FindBridge(Node outerNode, Node holeNode) {
        var p = outerNode;
        var hx = holeNode.x;
        var hy = holeNode.y;
        var qx = -System.Double.MaxValue;
        Node m = null;

        do {
            if (hy <= p.y && hy >= p.next.y && p.next.y != p.y) {
                var x = p.x + (hy - p.y) * (p.next.x - p.x) / (p.next.y - p.y);
                if (x <= hx && x > qx) {
                    qx = x;
                    if (x == hx) {
                        if (hy == p.y) return p;
                        if (hy == p.next.y) return p.next;
                    }
                    m = p.x < p.next.x ? p : p.next;
                }
            }
            p = p.next;
        } while (p != outerNode);

        if (m == null) return null;

        if (hx == qx) return m;

        var stop = m;
        var mx = m.x;
        var my = m.y;
        var tanMin = System.Double.MaxValue;
        double tan;

        p = m;
        
        do {
            if (hx >= p.x && p.x >= mx && hx != p.x &&
                PointInTriangle(hy < my ? hx : qx, hy, mx, my, hy < my ? qx : hx, hy, p.x, p.y)) {
                tan = System.Math.Abs(hy - p.y) / (hx - p.x);

                if (LocallyInside(p, holeNode) && (tan < tanMin || (tan == tanMin && (p.x > m.x || (p.x == m.x && SectorContainsSector(m, p)))))) {
                    m = p;
                    tanMin = tan;
                }
            }
            p = p.next;
        } while (p != stop);

        return m;
    }
    
    private static bool SectorContainsSector(Node m, Node p) {
        return Area(m.prev, m, p.prev) < 0 && Area(p.next, m, m.next) < 0;
    }
    
    private static void IndexCurve(Node startNode, double minX, double minY, double invSize) {
        var p = startNode;
        do {
            if (p.z == 0) p.z = ZOrder(p.x, p.y, minX, minY, invSize);
            p.prevZ = p.prev;
            p.nextZ = p.next;
            p = p.next;
        } while (p != startNode);

        p.prevZ.nextZ = null;
        p.prevZ = null;

        SortLinked(p);
    }
    
    private static Node SortLinked(Node list) {
        var p = list;
        var inSize = 1;

        do {
            var q = p;
            p = null;
            var tail = (Node) null;
            var numMerges = 0;
            while (q != null) {
                numMerges++;
                var qSize = inSize;
                var pSize = inSize;
                
                while (qSize > 0 || pSize > 0 && q != null) {
                    Node e;
                    if (qSize == 0) {
                        e = q;
                        q = q.nextZ;
                        pSize--;
                    } else if (pSize == 0 || q == null) {
                        e = p;
                        p = p.nextZ;
                        qSize--;
                    } else if (p.z <= q.z) {
                        e = p;
                        p = p.nextZ;
                        pSize--;
                    } else {
                        e = q;
                        q = q.nextZ;
                        qSize--;
                    }

                    if (tail != null) tail.nextZ = e;
                    else list = e;

                    e.prevZ = tail;
                    tail = e;
                }
                p = q;
            }

            tail.nextZ = null;
            inSize *= 2;
        } while (inSize < list.i);

        return list;
    }
    
    private static double ZOrder(double x, double y, double minX, double minY, double invSize) {
        x = 32767 * (x - minX) * invSize;
        y = 32767 * (y - minY) * invSize;

        long ix = (long)x;
        long iy = (long)y;

        ix = (ix | (ix << 8)) & 0x00FF00FF;
        ix = (ix | (ix << 4)) & 0x0F0F0F0F;
        ix = (ix | (ix << 2)) & 0x33333333;
        ix = (ix | (ix << 1)) & 0x55555555;

        iy = (iy | (iy << 8)) & 0x00FF00FF;
        iy = (iy | (iy << 4)) & 0x0F0F0F0F;
        iy = (iy | (iy << 2)) & 0x33333333;
        iy = (iy | (iy << 1)) & 0x55555555;

        return ix | (iy << 1);
    }

    private static Node GetLeftmost(Node startNode) {
        var p = startNode;
        var leftmost = startNode;
        do {
            if (p.x < leftmost.x || (p.x == leftmost.x && p.y < leftmost.y)) leftmost = p;
            p = p.next;
        } while (p != startNode);
        return leftmost;
    }
    
    private static bool PointInTriangle(double ax, double ay, double bx, double by, double cx, double cy, double px, double py) {
        return (cx - px) * (ay - py) - (ax - px) * (cy - py) >= 0 &&
               (ax - px) * (by - py) - (bx - px) * (ay - py) >= 0 &&
               (bx - px) * (cy - py) - (cx - px) * (by - py) >= 0;
    }
    
    private static bool IsValidDiagonal(Node a, Node b) {
        return a.next.i != b.i && a.prev.i != b.i && !IntersectsPolygon(a, b) &&
               (LocallyInside(a, b) && LocallyInside(b, a) && MiddleInside(a, b) &&
                (Area(a.prev, a, b.prev) != 0 || Area(a, b.prev, b) != 0) ||
                Equals(a, b) && Area(a.prev, a, a.next) > 0 && Area(b.prev, b, b.next) > 0);
    }
    
    private static double Area(Node p, Node q, Node r) {
        return (q.y - p.y) * (r.x - q.x) - (q.x - p.x) * (r.y - q.y);
    }
    
    private static bool Equals(Node p1, Node p2) {
        return p1.x == p2.x && p1.y == p2.y;
    }
    
    private static bool Intersects(Node p1, Node q1, Node p2, Node q2) {
        var o1 = Sign(Area(p1, q1, p2));
        var o2 = Sign(Area(p1, q1, q2));
        var o3 = Sign(Area(p2, q2, p1));
        var o4 = Sign(Area(p2, q2, q1));

        if (o1 != o2 && o3 != o4) return true;

        if (o1 == 0 && OnSegment(p1, p2, q1)) return true;
        if (o2 == 0 && OnSegment(p1, q2, q1)) return true;
        if (o3 == 0 && OnSegment(p2, p1, q2)) return true;
        if (o4 == 0 && OnSegment(p2, q1, q2)) return true;

        return false;
    }
    
    private static bool OnSegment(Node p, Node q, Node r) {
        if (q.x <= System.Math.Max(p.x, r.x) && q.x >= System.Math.Min(p.x, r.x) &&
            q.y <= System.Math.Max(p.y, r.y) && q.y >= System.Math.Min(p.y, r.y)) {
            return true;
        }
        return false;
    }

    private static int Sign(double num) {
        return num > 0 ? 1 : (num < 0 ? -1 : 0);
    }
    
    private static bool IntersectsPolygon(Node a, Node b) {
        var p = a;
        do {
            if (p.i != a.i && p.next.i != a.i && p.i != b.i && p.next.i != b.i && Intersects(p, p.next, a, b)) return true;
            p = p.next;
        } while (p != a);
        return false;
    }
    
    private static bool LocallyInside(Node a, Node b) {
        return Area(a.prev, a, a.next) < 0 ?
            Area(a, b, a.next) >= 0 && Area(a, a.prev, b) >= 0 :
            Area(a, b, a.prev) < 0 || Area(a, a.next, b) < 0;
    }
    
    private static bool MiddleInside(Node a, Node b) {
        var p = a;
        var inside = false;
        var px = (a.x + b.x) / 2;
        var py = (a.y + b.y) / 2;
        do {
            if (((p.y > py) != (p.next.y > py)) && p.next.y != p.y &&
                (px < (p.next.x - p.x) * (py - p.y) / (p.next.y - p.y) + p.x))
                inside = !inside;
            p = p.next;
        } while (p != a);
        return inside;
    }
    
    private static Node SplitPolygon(Node a, Node b) {
        var a2 = new Node(a.i, a.x, a.y);
        var b2 = new Node(b.i, b.x, b.y);
        var an = a.next;
        var bp = b.prev;

        a.next = b;
        b.prev = a;

        a2.next = an;
        an.prev = a2;

        b2.next = a2;
        a2.prev = b2;

        bp.next = b2;
        b2.prev = bp;

        return b2;
    }
    
    private static Node FilterPoints(Node start, Node end = null) {
        if (start == null) return null;
        if (end == null) end = start;

        var p = start;
        bool again;

        do {
            again = false;
            if (!p.steiner && (Equals(p, p.next) || Area(p.prev, p, p.next) == 0)) {
                RemoveNode(p);
                p = end = p.prev;
                if (p == p.next) break;
                again = true;
            } else {
                p = p.next;
            }
        } while (again || p != end);

        return end;
    }
    
    private static void RemoveNode(Node p) {
        p.next.prev = p.prev;
        p.prev.next = p.next;

        if (p.prevZ != null) p.prevZ.nextZ = p.nextZ;
        if (p.nextZ != null) p.nextZ.prevZ = p.prevZ;
    }
    
    private Node LinkedList(List<double> vertices, int start, int end, int dim, bool clockwise) {
        int i;
        Node last = null;

        if (clockwise == (SignedArea(vertices, start, end, dim) > 0)) {
            for (i = start; i < end; i += dim) last = InsertNode(i, vertices[i], vertices[i + 1], last);
        } else {
            for (i = end - dim; i >= start; i -= dim) last = InsertNode(i, vertices[i], vertices[i + 1], last);
        }

        if (last != null && Equals(last, last.next)) {
            RemoveNode(last);
            last = last.next;
        }
        
        return last;
    }

    private static Node InsertNode(int i, double x, double y, Node last) {
        var p = new Node(i, x, y);
        if (last == null) {
            p.prev = p;
            p.next = p;
        } else {
            p.next = last.next;
            p.prev = last;
            last.next.prev = p;
            last.next = p;
        }
        return p;
    }
    
    private static double SignedArea(List<double> data, int start, int end, int dim) {
        double sum = 0;
        for (int i = start, j = end - dim; i < end; i += dim) {
            sum += (data[j] - data[i]) * (data[i + 1] + data[j + 1]);
            j = i;
        }
        return sum;
    }
}