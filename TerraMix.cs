using UnityEngine;
using UnityEditor;

public class TerrainLayerMixerEditor : EditorWindow
{
    Terrain targetTerrain;

    TerrainLayer[] terrainLayers;

    int layerIndexA = 0;
    int layerIndexB = 1;

    float blendStrength = 0.5f;
    float brushRadius = 5f;
    bool brushActive = false;

    float opacityA = 0.3f;
    float opacityB = 0.7f;

    Vector2 scrollPosA;
    Vector2 scrollPosB;

    bool isPainting = false;

    // EditorPrefs Keys
    const string keyTerrainPath = "TLME_TargetTerrainPath";
    const string keyLayerA = "TLME_LayerIndexA";
    const string keyLayerB = "TLME_LayerIndexB";
    const string keyBlendStrength = "TLME_BlendStrength";
    const string keyBrushRadius = "TLME_BrushRadius";
    const string keyBrushActive = "TLME_BrushActive";
    const string keyOpacityA = "TLME_OpacityA";
    const string keyOpacityB = "TLME_OpacityB";

    [MenuItem("Tools/Terrain Layer Mixer")]
    public static void ShowWindow()
    {
        GetWindow<TerrainLayerMixerEditor>("Terrain Layer Mixer");
    }

    void OnEnable()
    {
        LoadSettings();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        SaveSettings();
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    void LoadSettings()
    {
        string terrainPath = EditorPrefs.GetString(keyTerrainPath, "");
        if (!string.IsNullOrEmpty(terrainPath))
        {
            targetTerrain = AssetDatabase.LoadAssetAtPath<Terrain>(terrainPath);
        }

        layerIndexA = EditorPrefs.GetInt(keyLayerA, 0);
        layerIndexB = EditorPrefs.GetInt(keyLayerB, 1);

        blendStrength = EditorPrefs.GetFloat(keyBlendStrength, 0.5f);
        brushRadius = EditorPrefs.GetFloat(keyBrushRadius, 5f);
        brushActive = EditorPrefs.GetBool(keyBrushActive, false);

        opacityA = EditorPrefs.GetFloat(keyOpacityA, 0.3f);
        opacityB = EditorPrefs.GetFloat(keyOpacityB, 0.7f);
    }

    void SaveSettings()
    {
        if (targetTerrain != null)
        {
            string path = AssetDatabase.GetAssetPath(targetTerrain);
            EditorPrefs.SetString(keyTerrainPath, path);
        }

        EditorPrefs.SetInt(keyLayerA, layerIndexA);
        EditorPrefs.SetInt(keyLayerB, layerIndexB);

        EditorPrefs.SetFloat(keyBlendStrength, blendStrength);
        EditorPrefs.SetFloat(keyBrushRadius, brushRadius);
        EditorPrefs.SetBool(keyBrushActive, brushActive);

        EditorPrefs.SetFloat(keyOpacityA, opacityA);
        EditorPrefs.SetFloat(keyOpacityB, opacityB);
    }

    void OnGUI()
    {
        GUILayout.Label("Terrain Layer Mixer Tool", EditorStyles.boldLabel);

        Terrain oldTerrain = targetTerrain;
        targetTerrain = (Terrain)EditorGUILayout.ObjectField("Target Terrain", targetTerrain, typeof(Terrain), true);

        if (targetTerrain != oldTerrain)
        {
            if (targetTerrain != null && targetTerrain.terrainData != null)
                terrainLayers = targetTerrain.terrainData.terrainLayers;
            else
                terrainLayers = null;

            if (terrainLayers != null)
            {
                if (layerIndexA >= terrainLayers.Length) layerIndexA = 0;
                if (layerIndexB >= terrainLayers.Length) layerIndexB = Mathf.Min(1, terrainLayers.Length - 1);
            }
        }

        if (targetTerrain == null)
        {
            EditorGUILayout.HelpBox("Bitte ein Terrain auswählen.", MessageType.Warning);
            return;
        }

        if (targetTerrain.terrainData == null)
        {
            EditorGUILayout.HelpBox("Das Terrain hat keine TerrainData.", MessageType.Error);
            return;
        }

        if (terrainLayers == null || terrainLayers.Length == 0)
        {
            EditorGUILayout.HelpBox("Das Terrain hat keine TerrainLayers.", MessageType.Error);
            return;
        }

        GUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();

        // Layer A Box
        EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(position.width / 2 - 10));
        GUILayout.Label("Layer A auswählen:", EditorStyles.boldLabel);

        scrollPosA = DrawLayerScrollView(scrollPosA, ref layerIndexA);
        EditorGUILayout.EndVertical();

        // Layer B Box
        EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(position.width / 2 - 10));
        GUILayout.Label("Layer B auswählen:", EditorStyles.boldLabel);

        scrollPosB = DrawLayerScrollView(scrollPosB, ref layerIndexB);
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);

        blendStrength = EditorGUILayout.Slider("Mix Stärke (Button)", blendStrength, 0f, 1f);

        GUILayout.Space(10);

        EditorGUILayout.LabelField("Brush Einstellungen:", EditorStyles.boldLabel);
        brushActive = EditorGUILayout.Toggle("Brush aktiv (Malmodus)", brushActive);
        brushRadius = EditorGUILayout.Slider("Brush Radius (Meter)", brushRadius, 1f, 50f);

        opacityA = EditorGUILayout.Slider("Layer A Opacity (Brush)", opacityA, 0f, 1f);
        opacityB = EditorGUILayout.Slider("Layer B Opacity (Brush)", opacityB, 0f, 1f);

        GUILayout.Space(10);

        if (GUILayout.Button("Layers sofort mischen (Button)"))
        {
            MixLayers();
        }

        if (targetTerrain != null)
        {
            EditorGUILayout.HelpBox("Im SceneView linksklick halten um zu malen (wenn Brush aktiv ist).", MessageType.Info);
        }
    }

    Vector2 DrawLayerScrollView(Vector2 scrollPos, ref int selectedIndex)
    {
        int previewSize = 50;
        int maxItemsToShow = 5;
        float scrollViewHeight = previewSize * maxItemsToShow + 10;

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(scrollViewHeight));
        if (terrainLayers == null || terrainLayers.Length == 0)
        {
            EditorGUILayout.LabelField("Keine Layers gefunden.");
            EditorGUILayout.EndScrollView();
            return scrollPos;
        }

        for (int i = 0; i < terrainLayers.Length; i++)
        {
            EditorGUILayout.BeginHorizontal();

            Texture2D preview = AssetPreview.GetAssetPreview(terrainLayers[i].diffuseTexture);
            if (preview == null)
                preview = EditorGUIUtility.IconContent("TerrainInspector.TerrainToolSettings").image as Texture2D;

            GUILayout.Label(preview, GUILayout.Width(previewSize), GUILayout.Height(previewSize));

            bool isSelected = (selectedIndex == i);
            bool toggle = GUILayout.Toggle(isSelected, terrainLayers[i].name, "Button", GUILayout.Height(previewSize));

            if (toggle && !isSelected)
                selectedIndex = i;

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        return scrollPos;
    }

    void MixLayers()
    {
        if (targetTerrain == null) return;
        if (terrainLayers == null || terrainLayers.Length == 0) return;
        if (layerIndexA == layerIndexB)
        {
            Debug.LogWarning("Layer A und Layer B dürfen nicht gleich sein!");
            return;
        }

        TerrainData terrainData = targetTerrain.terrainData;
        int w = terrainData.alphamapWidth;
        int h = terrainData.alphamapHeight;
        int layersCount = terrainData.alphamapLayers;

        float[,,] alphas = terrainData.GetAlphamaps(0, 0, w, h);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float valA = alphas[y, x, layerIndexA];
                float valB = alphas[y, x, layerIndexB];

                alphas[y, x, layerIndexA] = valA * (1f - blendStrength);
                alphas[y, x, layerIndexB] = valB + valA * blendStrength;

                float sum = 0f;
                for (int i = 0; i < layersCount; i++)
                    sum += alphas[y, x, i];

                if (sum > 0f)
                {
                    for (int i = 0; i < layersCount; i++)
                        alphas[y, x, i] /= sum;
                }
            }
        }

        terrainData.SetAlphamaps(0, 0, alphas);

        Debug.Log($"Layer '{terrainLayers[layerIndexA].name}' und '{terrainLayers[layerIndexB].name}' wurden mit Stärke {blendStrength} gemischt.");
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (!brushActive || targetTerrain == null)
            return;

        Event e = Event.current;

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        RaycastHit hit;

        if (!Physics.Raycast(ray, out hit))
            return;

        if (hit.collider.gameObject != targetTerrain.gameObject)
            return;

        Handles.color = new Color(1f, 0f, 0f, 0.4f);
        Handles.DrawSolidDisc(hit.point, Vector3.up, brushRadius);

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            isPainting = true;
            e.Use();
        }
        else if (e.type == EventType.MouseUp && e.button == 0)
        {
            isPainting = false;
            e.Use();
        }

        if (isPainting && (e.type == EventType.MouseDrag || e.type == EventType.MouseDown) && e.button == 0)
        {
            PaintAt(hit.point);
            e.Use();
        }

        if (isPainting)
            sceneView.Repaint();
    }

    void PaintAt(Vector3 worldPos)
    {
        if (targetTerrain == null) return;

        TerrainData terrainData = targetTerrain.terrainData;
        int alphamapWidth = terrainData.alphamapWidth;
        int alphamapHeight = terrainData.alphamapHeight;
        int layersCount = terrainData.alphamapLayers;

        Vector3 terrainPos = targetTerrain.transform.position;
        Vector3 relativePos = worldPos - terrainPos;

        int mapX = (int)((relativePos.x / terrainData.size.x) * alphamapWidth);
        int mapZ = (int)((relativePos.z / terrainData.size.z) * alphamapHeight);

        int radius = Mathf.RoundToInt((brushRadius / terrainData.size.x) * alphamapWidth);

        int xFrom = Mathf.Clamp(mapX - radius, 0, alphamapWidth - 1);
        int xTo = Mathf.Clamp(mapX + radius, 0, alphamapWidth - 1);
        int zFrom = Mathf.Clamp(mapZ - radius, 0, alphamapHeight - 1);
        int zTo = Mathf.Clamp(mapZ + radius, 0, alphamapHeight - 1);

        float[,,] alphas = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);

        for (int z = zFrom; z <= zTo; z++)
        {
            for (int x = xFrom; x <= xTo; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, z), new Vector2(mapX, mapZ));
                if (dist > radius) continue;

                float factor = 1f - (dist / radius);

                // Gemischte Werte nach Opacity und Blend
                float valA = alphas[z, x, layerIndexA];
                float valB = alphas[z, x, layerIndexB];

                float newValA = Mathf.Lerp(valA, opacityA, factor);
                float newValB = Mathf.Lerp(valB, opacityB, factor);

                alphas[z, x, layerIndexA] = newValA;
                alphas[z, x, layerIndexB] = newValB;

                // Normiere alle Layer an der Stelle
                float sum = 0f;
                for (int i = 0; i < layersCount; i++)
                    sum += alphas[z, x, i];

                if (sum > 0f)
                {
                    for (int i = 0; i < layersCount; i++)
                        alphas[z, x, i] /= sum;
                }
            }
        }

        terrainData.SetAlphamaps(0, 0, alphas);
    }
}
