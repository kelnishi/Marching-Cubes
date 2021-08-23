using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[CustomEditor(typeof(DensityGenerator), true)]
public class DensityGeneratorEditor : Editor
{
    private static readonly string[] _dontIncludeMe = new string[]{"m_Script", "dimensions"};
    
    //Preview Renderer
    private UnityEditor.Editor _textureEditor;
    private Texture3D _texture3D;

    private void OnEnable()
    {
        OnParametersUpdate();
    }

    void OnParametersUpdate()
    {
        DensityGenerator generator = target as DensityGenerator;

        Vector3Int generatorDimensions = generator.dimensions;
        int pixelCount = generatorDimensions.x * generatorDimensions.y * generatorDimensions.z;
        TextureFormat textureFormat = TextureFormat.RFloat;
        
        // Create the texture and apply the configuration
        _texture3D = new Texture3D(generatorDimensions.x, generatorDimensions.y, generatorDimensions.z, textureFormat, false);
        _texture3D.wrapMode = TextureWrapMode.Clamp;

        float[] data = generator.Generate();
        
        _texture3D.SetPixelData(data, 0);
        
        // Apply the changes to the texture and upload the updated texture to the GPU
        _texture3D.Apply(false);
    }

    public override void OnInspectorGUI()
    {
        EditorGUIUtility.labelWidth = 120f;
        serializedObject.Update();
        
        GUILayout.BeginVertical();
        EditorGUI.BeginChangeCheck();
 
        DrawPropertiesExcluding(serializedObject, _dontIncludeMe);
 
        GUILayout.Space(12f);
         
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("dimensions"),
            new GUIContent("Dimensions"),
            GUILayout.Height(32)
        );
        
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            OnParametersUpdate();
        }
        
        GUILayout.EndVertical();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Generate Texture3D"))
        {
            string path = EditorUtility.SaveFilePanelInProject("Save Texture3D", $"{target.GetType().Name}Field.asset", "asset",
                "Please enter a file name to save the texture to");
            if (path.Length != 0)
            {
                bool refresh = File.Exists(path);

                if (refresh)
                        AssetDatabase.DeleteAsset(path);
                
                AssetDatabase.CreateAsset(_texture3D, path);
                
                if (refresh)
                    AssetDatabase.Refresh();
            }
        }
            
        
        GUILayout.EndHorizontal();
    }
    
    #region Texture3D Preview
    //https://github.com/raphael-ernaelsten/Texture3DPreview-for-Unity
    #region Members
    /// <summary>
    /// The angle of the camera preview
    /// </summary>
    private Vector2 _cameraAngle = new Vector2(127.5f, -22.5f); // This default value will be used when rendering the asset thumbnail (see RenderStaticPreview)
    /// <summary>
    /// The raymarch interations
    /// </summary>
    private int _samplingIterations = 64;
    /// <summary>
    /// The factor of the Texture3D
    /// </summary>
    private float _density = 1;

    //// TODO : Investigate to access those variables as the default inspector is ugly
    //private SerializedProperty wrapModeProperty;
    //private SerializedProperty filterModeProperty;
    //private SerializedProperty anisotropyLevelProperty;
    #endregion

    #region Functions
    /// <summary>
    /// Sets back the camera angle
    /// </summary>
    public void ResetPreviewCameraAngle()
    {
        _cameraAngle = new Vector2(127.5f, -22.5f);
    }
    #endregion
    
    /// <summary>
    /// Tells if the Object has a custom preview
    /// </summary>
    public override bool HasPreviewGUI()
    {
        return true;
    }

    /// <summary>
    /// Draws the toolbar area on top of the preview window
    /// </summary>
    public override void OnPreviewSettings()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset Camera", EditorStyles.miniButton))
        {
            ResetPreviewCameraAngle();
        }
        EditorGUILayout.LabelField("Quality", GUILayout.MaxWidth(50));
        _samplingIterations = EditorGUILayout.IntPopup(_samplingIterations, new string[] { "16", "32", "64", "128", "256", "512" }, new int[] { 16, 32, 64, 128, 256, 512 }, GUILayout.MaxWidth(50));
        EditorGUILayout.LabelField("Density", GUILayout.MaxWidth(50));
        _density = EditorGUILayout.Slider(_density, 0, 5, GUILayout.MaxWidth(200));
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Draws the preview area
    /// </summary>
    /// <param name="rect">The area of the preview window</param>
    /// <param name="backgroundStyle">The default GUIStyle used for preview windows</param>
    public override void OnPreviewGUI(Rect rect, GUIStyle backgroundStyle)
    {
        _cameraAngle = PreviewRenderUtilityHelpers.DragToAngles(_cameraAngle, rect);

        if (Event.current.type == EventType.Repaint)
        {
            GUI.DrawTexture(rect, _texture3D.RenderTexture3DPreview(rect, _cameraAngle, 6.5f /*TODO : Find distance with fov and boundingsphere, when non uniform size will be supported*/, _samplingIterations, _density), ScaleMode.StretchToFill, true);
        }
    }

    /// <summary>
    /// Draws the custom preview thumbnail for the asset in the Project window
    /// </summary>
    /// <param name="assetPath">Path of the asset</param>
    /// <param name="subAssets">Array of children assets</param>
    /// <param name="width">Width of the rendered thumbnail</param>
    /// <param name="height">Height of the rendered thumbnail</param>
    /// <returns></returns>
    public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
    {
        return _texture3D.RenderTexture3DStaticPreview(new Rect(0, 0, width, height), _cameraAngle, 6.5f /*TODO : Find distance with fov and boundingsphere, when non uniform size will be supported*/, _samplingIterations, _density);
    }

    /// <summary>
    /// Allows to give a custom title to the preview window
    /// </summary>
    /// <returns></returns>
    public override GUIContent GetPreviewTitle()
    {
        return new GUIContent("DensityField preview");
    }
    #endregion
}

