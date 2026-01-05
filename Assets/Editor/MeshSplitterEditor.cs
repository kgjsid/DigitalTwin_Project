using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class MeshSplitterEditor : EditorWindow
{
    private TerrainMeshSplitter meshSplitter;
    private TerrainTextureExtractor textureExtractor;
    private MeshUVConverter meshUVConverter;

    private ComputeShader meshSplitterShader;
    private ComputeShader textureExtractorShader;

    private Object modelFile;
    private List<ConversionLog> conversionLogList = new List<ConversionLog>();

    private bool isFoldering;
    private bool isSplit;
    private bool meshSplitEnd;
    private bool textureSplitEnd;
    private bool saveDataEnd;

    [MenuItem("Tools/MeshSplitterEditor")]
    private static void ShowWindow()
    {
        GetWindow<MeshSplitterEditor>("MeshSplitterEditor");
    }

    private void OnGUI()
    {
        conversionLogList.Clear();
        modelFile = EditorGUILayout.ObjectField("Model File : ", modelFile, typeof(Object), false);
        isFoldering = EditorGUILayout.ToggleLeft("모델별 폴더링 실행", isFoldering);

        if (modelFile != null)
        {
            if(modelFile.GetType() != typeof(GameObject))
            {
                modelFile = null;
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(modelFile);

            if(!assetPath.Contains(".fbx"))
            {
                modelFile = null;
                return;
            }

            GameObject modelObject = modelFile as GameObject;
            MeshFilter meshFilter = modelObject.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = modelObject.GetComponent<MeshRenderer>();

            if (meshFilter == null || meshRenderer == null)
            {
                modelFile = null;
                return;
            }

            var fileName = assetPath.Substring(assetPath.LastIndexOf('/') + 1, assetPath.IndexOf(".fbx") - assetPath.LastIndexOf('/') - 1);
            conversionLogList.Add(new ConversionLog($"변경 대상 fbx 파일 : {fileName}"));

            if (GUILayout.Button("Split"))
            {
                FindComputeShader();

                if (meshSplitterShader == null || textureExtractorShader == null)
                    return;

                // ChangeModelReadable(assetPath, meshRenderer);

                meshSplitEnd = false;
                textureSplitEnd = false;
                saveDataEnd = false;

                CreateFolder(fileName);

                isSplit = true;

                meshSplitter = new TerrainMeshSplitter(meshSplitterShader);
                textureExtractor = new TerrainTextureExtractor(textureExtractorShader, fileName);

                conversionLogList.Add(new ConversionLog("Split"));

                meshSplitter.MeshSplit(meshFilter, meshRenderer, out Mesh[] newMeshList);
                meshSplitEnd = true;

                textureExtractor.ExtractTexture(meshFilter, meshRenderer, isFoldering, out Vector2Int[] startIndexList,
                                                out Vector2Int[] originTextureList, out Vector2Int[] newTextureList, out Material[] newMaterialList);
                textureSplitEnd = true;

                for (int i = 0; i < newMeshList.Length; i++)
                {
                    meshUVConverter = new MeshUVConverter(newMeshList[i]);

                    meshUVConverter.ConvertUV(originTextureList[i],
                                              newTextureList[i],
                                              startIndexList[i].x,
                                              startIndexList[i].y,
                                              out Mesh newMesh);
                    // newMesh. newTexture가 저장해야할 Mesh와 Texture
                    SaveMesh(newMesh, fileName, i, true);
                    SaveMaterial(newMaterialList[i], fileName, i);
                    SavePrefab(fileName, i);
                }
                saveDataEnd = true;
                isSplit = false;

                meshSplitter = null;
                textureExtractor = null;
                meshUVConverter = null;
            }

            System.GC.Collect();
        }
        ShowLog();
    }

    private void CreateFolder(string fileName)
    {
        if (!AssetDatabase.IsValidFolder("Assets/MeshFolder"))
            AssetDatabase.CreateFolder("Assets", "MeshFolder");

        if (isFoldering && !AssetDatabase.IsValidFolder($"Assets/MeshFolder/{fileName}"))
            AssetDatabase.CreateFolder("Assets/MeshFolder", fileName);

        if (!AssetDatabase.IsValidFolder("Assets/MaterialFolder"))
            AssetDatabase.CreateFolder("Assets", "MaterialFolder");

        if (isFoldering && !AssetDatabase.IsValidFolder($"Assets/MaterialFolder/{fileName}"))
            AssetDatabase.CreateFolder("Assets/MaterialFolder", fileName);

        if(!AssetDatabase.IsValidFolder("Assets/TextureFolder"))
            AssetDatabase.CreateFolder("Assets", "TextureFolder");

        if (isFoldering && !AssetDatabase.IsValidFolder($"Assets/TextureFolder/{fileName}"))
            AssetDatabase.CreateFolder("Assets/TextureFolder", fileName);

        if(!AssetDatabase.IsValidFolder($"Assets/TerrainPrefabFolder"))
            AssetDatabase.CreateFolder("Assets", "TerrainPrefabFolder");

        if (isFoldering && !AssetDatabase.IsValidFolder($"Assets/TerrainPrefabFolder/{fileName}"))
            AssetDatabase.CreateFolder("Assets/TerrainPrefabFolder", fileName);
    }

    private void ChangeModelReadable(string assetPath, MeshRenderer meshRenderer)
    {   
        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        ModelImporter modelImporter = importer as ModelImporter;
        modelImporter.isReadable = true;
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        List<Material> materialList = new List<Material>();
        meshRenderer.GetSharedMaterials(materialList);
        
        for(int i = 0; i < materialList.Count; i++)
        {
            string texturePath = AssetDatabase.GetAssetPath(materialList[i].GetTexture("_BaseMap"));
            importer = AssetImporter.GetAtPath(texturePath);
            TextureImporter textureImporter = importer as TextureImporter;
            textureImporter.isReadable = true;
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
        }
    }

    private void FindComputeShader()
    {
        meshSplitterShader = AssetDatabase.LoadAssetAtPath(AssetDatabase.GetAllAssetPaths().Where(path => path.Contains("MeshSplitCompute")).First(), typeof(ComputeShader)) as ComputeShader;
        textureExtractorShader = AssetDatabase.LoadAssetAtPath(AssetDatabase.GetAllAssetPaths().Where(path => path.Contains("TextureExtractorCompute")).First(), typeof(ComputeShader)) as ComputeShader;
    }

    private void SaveMesh(Mesh mesh, string fileName, int subMeshIndex, bool optimizeMesh = true)
    {
        string path = isFoldering ? $"Assets/MeshFolder/{fileName}/{fileName}_{subMeshIndex}.asset"
                                  : $"Assets/MeshFolder/{fileName}_{subMeshIndex}.asset";

        if (optimizeMesh)
            MeshUtility.Optimize(mesh);

        MeshUtility.SetMeshCompression(mesh, ModelImporterMeshCompression.Off);
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
    }

    private void SaveMaterial(Material material, string fileName, int subMeshIndex)
    {
        string path = isFoldering ? $"Assets/MaterialFolder/{fileName}/{fileName}_{subMeshIndex}.mat"
                                  : $"Assets/MaterialFolder/{fileName}_{subMeshIndex}.mat";

        string texturePath = isFoldering ? $"Assets/TextureFolder/{fileName}/{fileName}_{subMeshIndex}.jpg"
                                         : $"Assets/TextureFolder/{fileName}_{subMeshIndex}.jpg";

        var texture = AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture)) as Texture;
        material.SetTexture("_BaseMap", texture);

        AssetDatabase.CreateAsset(material, path);
        AssetDatabase.SaveAssets();
    }

    private void SavePrefab(string fileName, int subMeshIndex)
    {
        string fileNumber = fileName.Substring(fileName.LastIndexOf('_') + 1);

        string prefabPath = isFoldering ? $"Assets/TerrainPrefabFolder/{fileName}/Terrains({fileNumber})_{subMeshIndex}.prefab"
                                        : $"Assets/TerrainPrefabFolder/Terrains({fileNumber})_{subMeshIndex}.prefab";

        string meshPath = isFoldering ? $"Assets/MeshFolder/{fileName}/{fileName}_{subMeshIndex}.asset"
                                      : $"Assets/MeshFolder/{fileName}_{subMeshIndex}.asset";

        string materialPath = isFoldering ? $"Assets/MaterialFolder/{fileName}/{fileName}_{subMeshIndex}.mat"
                                          : $"Assets/MaterialFolder/{fileName}_{subMeshIndex}.mat";

        var mesh = AssetDatabase.LoadAssetAtPath(meshPath, typeof(Mesh)) as Mesh;
        var material = AssetDatabase.LoadAssetAtPath(materialPath, typeof(Material)) as Material;

        if(material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") == null)
        {
            string texturePath = isFoldering ? $"Assets/TextureFolder/{fileName}/{fileName}_{subMeshIndex}.jpg"
                                             : $"Assets/TextureFolder/{fileName}_{subMeshIndex}.jpg";
            var texture = AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture)) as Texture;
            material.SetTexture("_BaseMap", texture);
        }

        GameObject terrainObject = new GameObject($"{fileName}_{subMeshIndex}");
        terrainObject.transform.position = Vector3.zero;
        terrainObject.transform.rotation = Quaternion.identity;

        terrainObject.AddComponent<MeshFilter>().mesh = mesh;
        terrainObject.AddComponent<MeshRenderer>().material = material;
        terrainObject.AddComponent<MeshCollider>();

        PrefabUtility.SaveAsPrefabAsset(terrainObject, prefabPath);
        AssetDatabase.Refresh();

        DestroyImmediate(terrainObject);
    }

    private void ShowLog()
    {
        EditorGUILayout.Space();
        // if (isSplit) 
        // {
        //     if (meshSplitEnd) conversionLogList.Add(new ConversionLog("meshSplitEnd")); else conversionLogList.Add(new ConversionLog("meshSplit"));
        //     if (textureSplitEnd) conversionLogList.Add(new ConversionLog("textureSplitEnd")); else conversionLogList.Add(new ConversionLog("textureSplit"));
        //     if (!saveDataEnd) conversionLogList.Add(new ConversionLog("SaveData"));
        // }
        // if (saveDataEnd)
        //     conversionLogList.Add(new ConversionLog("SaveDataEnd"));

        for (int i = 0; i < conversionLogList.Count; i++)
        {
            EditorGUILayout.LabelField(conversionLogList[i].log);
        }
    }

    [System.Serializable]
    private struct ConversionLog
    {
        public string log;

        public ConversionLog(string log)
        {
            this.log = log;
        }
    }
}
