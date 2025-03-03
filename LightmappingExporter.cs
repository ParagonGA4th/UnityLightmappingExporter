using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using System.Xml;
using static UnityEngine.InputManagerEntry;
using System.Text;
using static UnityEditor.ObjectChangeEventStream;
using System.Linq;
using System.IO;

//Exporter Used For Stolen Youth (2024)
public class LightmappingExporter : EditorWindow
{
    enum SelectingType : ushort
    {
        AllWithRenderer,
        OnlySelected
    }

    private List<MeshRenderer> GetObjectsWithSelectingType(SelectingType type)
    {
        List<MeshRenderer> tRet = new List<MeshRenderer>();

        if (type == SelectingType.AllWithRenderer)
        {
            //AllWithRenderer
            GameObject[] objList = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject rootObj in objList)
            {
                foreach (Transform tr in rootObj.GetComponentsInChildren<Transform>())
                {
                    //자식 있으면 역시 처리.
                    //재귀적으로 들어가야 하는데.. 유니티에서는 GetComponentsInChildren이 이걸 미리 해준다!
                    //자신 역시 포함됨. 그대로 돌려도 된다.
                    MeshRenderer ren = tr.gameObject.GetComponent<MeshRenderer>();
                    if (ren != null)
                    {
                        tRet.Add(ren);
                    }
                }
            }
        }
        else
        {
            //OnlySelected
            foreach (GameObject obj in Selection.gameObjects)
            {
                MeshRenderer ren = obj.GetComponent<MeshRenderer>();
                if (ren != null)
                {
                    tRet.Add(ren);
                }
            }
        }

        return tRet;
    }

    SelectingType selectingType = SelectingType.AllWithRenderer;
    string tFileName = "";
    string tFilePath = "";

    [MenuItem("Stolen Youth/Paragon Lightmapping Exporter")]
    public static void ShowWindow()
    {
        GetWindow<LightmappingExporter>("Paragon Lightmapping Exporter");
    }

    private void OnGUI()
    {
        //에디터 윈도우 코드
        GUILayout.Label("Paragon Lightmapping Exporter", EditorStyles.boldLabel);
        string tSceneText = "Will Export : ";
        tSceneText += SceneManager.GetActiveScene().name;
        GUILayout.Label(tSceneText, EditorStyles.largeLabel);
        selectingType = (SelectingType)EditorGUILayout.EnumPopup("Target", selectingType);
        tFilePath = EditorGUILayout.TextField("File Path", tFilePath);
        tFileName = EditorGUILayout.TextField("XML Name", tFileName);

        if (GUILayout.Button("Before Baking Lightmaps Click"))
        {
            //라이트매핑에 필요한 Contribute GI 플래그 키기.
            foreach (MeshRenderer ren in GetObjectsWithSelectingType(selectingType))
            {
                //Lightmapping 퀄리티 업.
                ren.stitchLightmapSeams = true;

                //라이트매핑 가능하게 하는 플래그.
                var flags = StaticEditorFlags.ContributeGI;
                GameObjectUtility.SetStaticEditorFlags(ren.gameObject, flags);
            }
        }

        if (GUILayout.Button("Export Lightmaps"))
        {
            var tBuilder = new StringBuilder();
            tBuilder.Append(tFilePath);
            tBuilder.Append('\\');
            tBuilder.Append(tFileName);
            tBuilder.Append(".pglightmap");

            string tFiller = "Saving To : ";
            tFiller += tBuilder.ToString();
            Debug.Log(tFiller);
            WriteXMLFile(GetObjectsWithSelectingType(selectingType), tBuilder.ToString());
        }
    }

    private void WriteXMLFile(List<MeshRenderer> rendererList, string xmlpath)
    {
        //Lightmapping Index, Name 등등은 분리될 것.
        //결과적으로 필요하니, 정보를 따로 분류해서 보관해야 한다.
        //ID & 파일 이름 + Scaling + Offset...
        XmlDocument xmlDoc = new XmlDocument();

        // 루트 노드 생성       
        XmlNode tRootNode = xmlDoc.CreateNode(XmlNodeType.Element, "Root", string.Empty);
        xmlDoc.AppendChild(tRootNode);

        // 자식 노드 생성       
        {
            XmlNode tLightmapList = xmlDoc.CreateNode(XmlNodeType.Element, "LightmapList", string.Empty);
            tRootNode.AppendChild(tLightmapList);

            //LightmapsMode.NonDirectional ONLY.
            //그러니, LightmapData.lightmapColor만 저장할 수 있을 것.
            //인덱스 순서대로 기록한다.
            for (int i = 0; i < LightmapSettings.lightmaps.Length; i++)
            {
                XmlNode tLightmapColorName = xmlDoc.CreateNode(XmlNodeType.Element, "LightmapColorName", string.Empty);
                string tEXRName = LightmapSettings.lightmaps[i].lightmapColor.name;
                tEXRName += ".exr";
                tLightmapColorName.InnerText = tEXRName;
                tLightmapList.AppendChild(tLightmapColorName);
            }
        }
        {
            XmlNode tRenderObjectList = xmlDoc.CreateNode(XmlNodeType.Element, "RenderObjectList", string.Empty);
            tRootNode.AppendChild(tRenderObjectList);

            //개별 오브젝트와 해당 오브젝트의 이름을 담아서 매칭하기 위해.
            foreach (MeshRenderer ren in rendererList)
            {
                XmlNode tRenderObject = xmlDoc.CreateNode(XmlNodeType.Element, "RenderObject", string.Empty);
                tRenderObjectList.AppendChild(tRenderObject);

                //RenderObject Name
                XmlNode tRoName = xmlDoc.CreateNode(XmlNodeType.Element, "ObjectName", string.Empty);

                if (PrefabUtility.IsPartOfAnyPrefab(ren.gameObject))
                {
                    tRoName.InnerText = PrefabUtility.GetOutermostPrefabInstanceRoot(ren.gameObject).name;
                }
                else
                {
                    tRoName.InnerText = ren.gameObject.name;
                }

                tRenderObject.AppendChild(tRoName);

                //Mesh Name
                XmlNode tMeshName = xmlDoc.CreateNode(XmlNodeType.Element, "MeshName", string.Empty);
                MeshFilter fil = ren.gameObject.GetComponent<MeshFilter>();
                if (fil != null)
                {
                    string assetPath = AssetDatabase.GetAssetPath(fil.sharedMesh);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        tMeshName.InnerText = Path.GetFileName(assetPath);
                    }
                    else
                    {
                        Debug.LogError($"No Asset Path For {fil.sharedMesh.name}");
                        tMeshName.InnerText = "NULL";
                    }
                }
                else
                {
                    Debug.LogError($"No Mesh Filter For {ren.gameObject.name}");
                }
                tRenderObject.AppendChild(tMeshName);

                //LightmapIndex
                XmlNode tLightmapIndex = xmlDoc.CreateNode(XmlNodeType.Element, "LightmapIndex", string.Empty);
                tLightmapIndex.InnerText = ren.lightmapIndex.ToString();
                tRenderObject.AppendChild(tLightmapIndex);

                //Scale & Offset
                Vector4 tScaleOffset = ren.lightmapScaleOffset;
                {
                    XmlNode tUVScale = xmlDoc.CreateNode(XmlNodeType.Element, "UVScale", string.Empty);
                    tRenderObject.AppendChild(tUVScale);

                    XmlNode tsX = xmlDoc.CreateNode(XmlNodeType.Element, "x", string.Empty);
                    tsX.InnerText = tScaleOffset.x.ToString("F6");
                    tUVScale.AppendChild(tsX);

                    XmlNode tsY = xmlDoc.CreateNode(XmlNodeType.Element, "y", string.Empty);
                    tsY.InnerText = tScaleOffset.y.ToString("F6");
                    tUVScale.AppendChild(tsY);
                }
                {
                    XmlNode tUVOffset = xmlDoc.CreateNode(XmlNodeType.Element, "UVOffset", string.Empty);
                    tRenderObject.AppendChild(tUVOffset);

                    XmlNode toX = xmlDoc.CreateNode(XmlNodeType.Element, "x", string.Empty);
                    toX.InnerText = tScaleOffset.z.ToString("F6");
                    tUVOffset.AppendChild(toX);

                    XmlNode toY = xmlDoc.CreateNode(XmlNodeType.Element, "y", string.Empty);
                    toY.InnerText = tScaleOffset.w.ToString("F6");
                    tUVOffset.AppendChild(toY);
                }
            }
        }
        //역슬래시 추가해야. 
        xmlDoc.Save(xmlpath);
    }
}
