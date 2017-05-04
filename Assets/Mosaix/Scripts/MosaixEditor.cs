// This isn't in an Editor directory due to Unity's broken compile order handling, which
// makes it impossible to access Editor scripts from non-Editor scripts.  Instead, we just
// conditionally compile this.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[CustomEditor(typeof(Mosaix))]
public class MosaixEditor: Editor
{
    static Material TextureDisplayMaterial;

    public enum TextureDisplayMode
    {
        Normal,
        ShowAlphaOnly,
        ShowWithoutAlpha,
        ShowUnpremultiplied,
    };

    // These are transient settings that affect the inspector only.  We store these on the Mosaix object
    // itself so they're preserved when the object is deselected and reselected, but they're Nonserialized
    // so they aren't saved with the scene, which would cause annoying noise in version control.
    public class EditorSettings
    {
        public bool ShowShaders = false;
        public bool ShowMasking = false;
        public bool ShowAdvanced = false;
        public bool ShowDebugging = false;

        public int DisplayedTexture = 0;
        public bool ScaleTexture = true;
        public TextureDisplayMode DisplayMode = TextureDisplayMode.Normal;
    };

    public override void OnInspectorGUI()
    {
        if(TextureDisplayMaterial == null)
            TextureDisplayMaterial = new Material(Shader.Find("Hidden/Mosaix/EditorTextureDisplay"));

        GUIStyle boldFoldout = new GUIStyle(EditorStyles.foldout);
        boldFoldout.fontStyle = FontStyle.Bold;

        Mosaix obj = (Mosaix) target;
        Undo.RecordObject(obj, "Mosaix change");
        
        EditorGUILayout.LabelField("Basic settings", EditorStyles.boldLabel);
        obj.MosaicLayer = EditorGUILayout.LayerField("Mosaic Layer", obj.MosaicLayer);
        obj.MosaicBlocks = EditorGUILayout.IntSlider("Mosaic Blocks", obj.MosaicBlocks, 1, 100);
        
        obj.EditorSettings.ShowMasking = EditorGUILayout.Foldout(obj.EditorSettings.ShowMasking, "Masking", true, boldFoldout);
        if(obj.EditorSettings.ShowMasking)
        {
            obj.TextureMasking = EditorGUILayout.ToggleLeft("Texture masking", obj.TextureMasking);
            if(obj.TextureMasking)
            {
                ++EditorGUI.indentLevel;
                obj.MaskingTexture = (Texture) EditorGUILayout.ObjectField("Masking Texture", obj.MaskingTexture, typeof(Texture), true);
                --EditorGUI.indentLevel;
            }

            obj.SphereMasking = EditorGUILayout.ToggleLeft("Sphere masking", obj.SphereMasking);
            if(obj.SphereMasking)
            {
                ++EditorGUI.indentLevel;
                obj.MaskingSphere = (GameObject) EditorGUILayout.ObjectField("Masking Sphere", obj.MaskingSphere, typeof(GameObject), true);
                obj.MaskFade = EditorGUILayout.Slider("Mask Fade", obj.MaskFade, 0, 1);
                --EditorGUI.indentLevel;
            }
        }

        obj.EditorSettings.ShowAdvanced = EditorGUILayout.Foldout(obj.EditorSettings.ShowAdvanced, "Advanced settings", true, boldFoldout);
        if(obj.EditorSettings.ShowAdvanced)
        {
            ++EditorGUI.indentLevel;
            obj.ShadowsCastOnMosaic = EditorGUILayout.Toggle("Shadows Cast On Mosaic", obj.ShadowsCastOnMosaic);
            obj.HighResolutionRender = EditorGUILayout.Toggle("High Resolution Render", obj.HighResolutionRender);
            obj.Alpha = EditorGUILayout.Slider("Alpha", obj.Alpha, 0, 1);
            --EditorGUI.indentLevel;
        }

        obj.EditorSettings.ShowShaders = EditorGUILayout.Foldout(obj.EditorSettings.ShowShaders, "Shaders", true, boldFoldout);

        if(obj.EditorSettings.ShowShaders)
        {
            ++EditorGUI.indentLevel;
            obj.MosaicMaterial = (Material) EditorGUILayout.ObjectField("Mosaic Material", obj.MosaicMaterial, typeof(Material), false);
            obj.ExpandEdgesShader = (Shader) EditorGUILayout.ObjectField("Expand Edges Shader", obj.ExpandEdgesShader, typeof(Shader), false);
            --EditorGUI.indentLevel;
        }

        if(EditorApplication.isPlaying)
        {
            // For development, allow inspecting the various textures used by the shader.
            obj.EditorSettings.ShowDebugging = EditorGUILayout.Foldout(obj.EditorSettings.ShowDebugging, "Debugging", true, boldFoldout);

            if(obj.EditorSettings.ShowDebugging)
            {
                ++EditorGUI.indentLevel;
                obj.ExpandPasses = EditorGUILayout.IntSlider("Expand Passes", obj.ExpandPasses, 0, 5);

                List<RenderTexture> TexturePasses = obj.GetTexturePasses();
                
                int NumTextures = TexturePasses.Count;
                obj.EditorSettings.DisplayedTexture = EditorGUILayout.IntSlider("Texture", obj.EditorSettings.DisplayedTexture, 0, NumTextures-1);

                obj.EditorSettings.ScaleTexture = EditorGUILayout.Toggle("Scale texture", obj.EditorSettings.ScaleTexture);
                obj.EditorSettings.DisplayMode = (TextureDisplayMode) EditorGUILayout.EnumPopup("Display Mode", obj.EditorSettings.DisplayMode);

                if(TexturePasses.Count != 0)
                {
                    // Wrap this in a BeginHorizontal, so we can use FlexibleSpace to center the texture.
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    
                    // Wrap in a BeginVertical.  This makes the space for the texture stay the same.  If ScaleTexture
                    // is turned off, we want to make the texture display smaller, but not take up less space in the
                    // UI or everything will move around constantly when changing textures.
                    EditorGUILayout.BeginVertical(new GUILayoutOption[] {
                            GUILayout.MinHeight(TexturePasses[0].height),
                            GUILayout.MinWidth(TexturePasses[0].width),
                    });

                    int ScaleTextureIndex = obj.EditorSettings.ScaleTexture? 0:obj.EditorSettings.DisplayedTexture;
                    // Begin the BeginVertical block that will contain only the texture.
                    Rect r = EditorGUILayout.BeginVertical(new GUILayoutOption[] {
                            GUILayout.MinHeight(TexturePasses[ScaleTextureIndex].height),
                            GUILayout.MinWidth(TexturePasses[ScaleTextureIndex].width),
                    });

                    // There's no EditorGUILayout for drawing textures.  Insert a space, to tell layout
                    // about the space we need for the texture display.
                    GUILayout.Space(TexturePasses[ScaleTextureIndex].height);

                    Texture tex = TexturePasses[obj.EditorSettings.DisplayedTexture];

                    // Save the filter mode and switch to nearest neighbor to draw it in the editor.
                    FilterMode SavedFilterMode = tex.filterMode;
                    tex.filterMode = FilterMode.Point;

                    // Note that we don't use DrawTextureAlpha or the default DrawPreviewTexture material.
                    // These seem to perform some kind of unwanted color management to the texture and don't
                    // show us what's really in it (eg. 1.0 alpha outputs as 0.8).  We just use our own
                    // material for all display modes.
                    Material mat = TextureDisplayMaterial;
                    foreach(string keyword in mat.shaderKeywords)
                        mat.DisableKeyword(keyword);
                    switch(obj.EditorSettings.DisplayMode)
                    {
                    case TextureDisplayMode.Normal:
                        mat.EnableKeyword("DISP_NORMAL");
                        break;
                    case TextureDisplayMode.ShowAlphaOnly:
                        mat.EnableKeyword("DISP_ALPHA_ONLY");
                        break;
                    case TextureDisplayMode.ShowWithoutAlpha:
                        mat.EnableKeyword("DISP_WITHOUT_ALPHA");
                        break;
                    case TextureDisplayMode.ShowUnpremultiplied:
                        mat.EnableKeyword("DISP_UNPREMULTIPLY");
                        break;
                    }
                    
                    EditorGUI.DrawPreviewTexture(r, tex, mat);
                    tex.filterMode = SavedFilterMode;
                    
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndVertical();
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
                --EditorGUI.indentLevel;
            }
        }
    }
}
#endif

