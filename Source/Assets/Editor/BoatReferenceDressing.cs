using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class BoatReferenceDressing
{
    const string Folder="Assets/03.Resources/BoatSurface";
    const string Request="Library/BoatReferenceDress.request";
    static Transform decor;
    static System.Random rng;
    static BoatReferenceDressing(){EditorApplication.delayCall+=Run;}
    static void Run(){if(!File.Exists(Request))return;if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Run;return;}string action=File.ReadAllText(Request).Trim();File.Delete(Request);try{if(action=="clean-boat")CleanBoat();else Dress();}catch(Exception e){File.WriteAllText("Library/BoatReferenceDress.result.txt",e.ToString());Debug.LogException(e);}}
    [MenuItem("Tools/C Team/Dress Boat Scene Like Reference")]
    public static void Dress()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/01.Scenes/BoatTest.unity")throw new InvalidOperationException("Open BoatTest first.");
        Directory.CreateDirectory("Library/BoatSurfaceBackup");
        File.Copy(scene.path,"Library/BoatSurfaceBackup/BoatTest-before-reference-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
        var existing=GameObject.Find("Reference Dressing");bool previouslyDressed=existing!=null;bool layoutV2=existing&&existing.transform.Find("Reference Layout Version 2");if(existing)UnityEngine.Object.DestroyImmediate(existing);
        decor=new GameObject("Reference Dressing").transform;rng=new System.Random(4821);
        var islands=GameObject.Find("Limestone Islands and Palms");
        if(!previouslyDressed&&!File.Exists("Library/BoatReferenceDress.applied"))
        {
            foreach(Transform child in islands.transform){var b=B(child.gameObject);float side=b.center.x<0?-1:1;child.position+=new Vector3(side*14,0,50);}
            var vessel=GameObject.Find("Fishing Boat — Foreground");vessel.transform.position+=new Vector3(-3,0,-11);
            GameObject.Find("Yellow Dive Canopy and Deck Equipment").transform.position+=new Vector3(-3,0,-11);
            foreach(var go in scene.GetRootGameObjects())foreach(var t in go.GetComponentsInChildren<Transform>())if(t.name=="Boat Wake")t.position+=new Vector3(-3,0,-11);
        }
        if(!layoutV2){foreach(Transform child in islands.transform){var b=B(child.gameObject);child.position+=new Vector3(b.center.x<0?-20:20,0,15);}}
        new GameObject("Reference Layout Version 2").transform.SetParent(decor);
        var camera=Camera.main;camera.farClipPlane=550;
        var sky=GameObject.Find("Sky and Soft Clouds");sky.transform.position=new Vector3(0,150,420);sky.transform.localScale=new Vector3(1000,600,1);
        var ocean=GameObject.Find("Turquoise Ocean");ocean.transform.position=new Vector3(0,-.35f,140);ocean.transform.localScale=new Vector3(65,1,65);
        RenderSettings.fogStartDistance=200;RenderSettings.fogEndDistance=420;

        // Map prefabs remain connected to their original rock materials.
        var greenery=new GameObject("Map Foliage on Limestone Ledges").transform;greenery.SetParent(decor);
        var outer=new GameObject("Map Cliffs Filling Outer Coast").transform;outer.SetParent(decor);
        var surfaces=new List<Transform>();foreach(Transform c in islands.transform)surfaces.Add(c);
        for(int side=-1;side<=1;side+=2)for(int i=0;i<4;i++)
        {
            float x=side*(126+i*13),z=147+i*9,height=(side<0?68:61)-i*4;
            var c=Prefab("P_CliffClassic"+((i%6)+1),new Vector3(x,-1.4f,z),new Vector3(22,height,24),outer,false);c.name="Map Cliff — Outer Coast";surfaces.Add(c.transform);
            P("Outer Shore",PrimitiveType.Sphere,new Vector3(x,-.2f,z),new Vector3(25,.7f,27),M("Reference Beach",new Color(.79f,.72f,.45f)),outer);
            for(int p=0;p<2;p++)
            {
                var tree=Prefab("P_CoconutPalm01",new Vector3(x+Range(-7,7),height-1.3f,z),new Vector3(6,7,6),outer,true);
                foreach(var r in tree.GetComponentsInChildren<Renderer>()){var mm=r.sharedMaterials;for(int k=0;k<mm.Length;k++)mm[k]=M(mm[k]&&mm[k].name.Contains("Bark")?"Reference Palm Trunk":"Reference Palm Leaves",mm[k]&&mm[k].name.Contains("Bark")?new Color(.42f,.28f,.11f):new Color(.21f,.55f,.065f));r.sharedMaterials=mm;}
            }
        }
        var plantMats=new Dictionary<Material,Material>();int bushCount=0;
        foreach(Transform cliff in surfaces)
        {
            bool shelf=cliff.name.StartsWith("Map Rock Shelf"),column=cliff.name.StartsWith("Map Cliff");
            if(!shelf&&!column)continue;
            var b=B(cliff.gameObject);int count=shelf?14:20;
            for(int i=0;i<count;i++)
            {
                float x=b.center.x+Range(-.40f,.40f)*b.size.x;
                float z=b.center.z+Range(-.30f,.30f)*b.size.z;
                var bush=Prefab(i%3==0?"P_Bush2_Light":"P_BushTropical01",new Vector3(x,b.max.y-.15f,z),new Vector3(Range(7,11),Range(2.5f,4.0f),Range(5,8)),greenery,false);
                foreach(var r in bush.GetComponentsInChildren<Renderer>())
                {
                    var mats=r.sharedMaterials;
                    for(int m=0;m<mats.Length;m++)
                    {
                        var old=mats[m];if(!old)continue;
                        if(!plantMats.TryGetValue(old,out var fresh))
                        {fresh=M("Reference Foliage "+old.name.Replace("/","_"),old.name.Contains("Bark")?new Color(.34f,.25f,.12f):new Color(.36f,.72f,.075f));fresh.SetFloat("_Cull",0);plantMats.Add(old,fresh);}
                        mats[m]=fresh;
                    }r.sharedMaterials=mats;
                }bushCount++;
            }
        }
        // Repaint only this boat instance, retaining the model and its texture detail.
        var painted=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Reference Cyan Boat.mat");
        if(!painted){painted=new Material(Shader.Find("BoatSurface/ReferenceBoat"));AssetDatabase.CreateAsset(painted,Folder+"/Reference Cyan Boat.mat");}
        painted.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/99.StoreAssets/Boat/Meshy_AI_Low_Poly_Fishing_Boat_1011110927_texture_fbx/Meshy_AI_Low_Poly_Fishing_Boat_1011110927_texture.png"));
        foreach(var r in GameObject.Find("Fishing Boat — Foreground").GetComponentsInChildren<Renderer>())r.sharedMaterial=painted;
        AddRestaurant();
        // Small sea birds form a background detail rather than any screen overlay.
        var bird=M("Reference Seabird",new Color(.94f,1,1));
        for(int i=0;i<8;i++){var p=new Vector3(Range(-32,62),Range(25,44),Range(150,230));Rod("Seabird Wing",p,p+new Vector3(-.55f,.25f,0),.06f,bird,decor);Rod("Seabird Wing",p,p+new Vector3(.55f,.25f,0),.06f,bird,decor);}
        File.WriteAllText("Library/BoatReferenceDress.applied","1");
        EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        Preview(camera);
        File.WriteAllText("Library/BoatReferenceDress.result.txt","SUCCESS: Reference dressing saved. Map foliage instances: "+bushCount+"; UI canvases: "+UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length);
    }
    [MenuItem("Tools/C Team/Remove Added Boat Props and Crew")]
    public static void CleanBoat()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/01.Scenes/BoatTest.unity")throw new InvalidOperationException("Open BoatTest first.");
        Directory.CreateDirectory("Library/BoatSurfaceBackup");
        File.Copy(scene.path,"Library/BoatSurfaceBackup/BoatTest-before-clean-boat-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
        var additions=GameObject.Find("Boat Rails Floats Ladder and Equipment");if(additions)Undo.DestroyObjectImmediate(additions);
        var canopy=GameObject.Find("Yellow Dive Canopy and Deck Equipment");
        if(canopy){var remove=new List<GameObject>();foreach(Transform child in canopy.transform)if(child.name=="Dive Cylinder")remove.Add(child.gameObject);foreach(var o in remove)Undo.DestroyObjectImmediate(o);}
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Preview(Camera.main);
        File.WriteAllText("Library/BoatReferenceDress.result.txt","SUCCESS: Added crew, fish decorations, fenders, ladder and deck props removed. Boat and yellow canopy preserved.");
    }
    static void AddCrew(Transform parent,Material blue,Material gold)
    {
        var skin=M("Reference Crew Skin",new Color(.82f,.54f,.31f));var suit=M("Reference Crew Wetsuit",new Color(.025f,.12f,.22f));var visor=M("Reference Diving Visor",new Color(.025f,.07f,.09f));
        for(int i=0;i<2;i++)
        {
            var holder=new GameObject(i==0?"Deck Diver":"Boat Captain").transform;holder.SetParent(parent);float x=-14+i*5;
            P("Crew Wetsuit Torso",PrimitiveType.Capsule,new Vector3(x,4.8f,-14),new Vector3(1.35f,.8f,.8f),i==0?blue:suit,holder);
            P("Crew Head",PrimitiveType.Sphere,new Vector3(x,6,-14),new Vector3(.82f,.9f,.82f),skin,holder);
            P("Dive Mask",PrimitiveType.Cube,new Vector3(x,6.05f,-14.44f),new Vector3(.75f,.32f,.18f),visor,holder);
            P("Yellow Cap",PrimitiveType.Sphere,new Vector3(x,6.43f,-14),new Vector3(.92f,.28f,.9f),gold,holder);
            for(int j=0;j<2;j++){float side=j==0?-1:1;Rod("Crew Leg",new Vector3(x+side*.3f,4.3f,-14),new Vector3(x+side*.4f,3.3f,-14),.35f,suit,holder);P("Crew Boot",PrimitiveType.Cube,new Vector3(x+side*.4f,3.3f,-14.15f),new Vector3(.48f,.28f,.7f),visor,holder);Rod("Crew Arm",new Vector3(x+side*.65f,5.2f,-14),new Vector3(x+side*.8f,4.4f,-14.25f),.3f,blue,holder);P("Crew Hand",PrimitiveType.Sphere,new Vector3(x+side*.8f,4.3f,-14.25f),new Vector3(.35f,.35f,.35f),skin,holder);}
            P("Yellow Dive Tank",PrimitiveType.Capsule,new Vector3(x,4.9f,-13.45f),new Vector3(.65f,.8f,.65f),gold,holder);
        }
    }
    static void AddRestaurant()
    {
        var old=GameObject.Find("Distant Coastal Restaurant");if(old)old.SetActive(false);
        var parent=new GameObject("Coastal Sushi Restaurant — Decorated").transform;parent.SetParent(decor);
        var timber=M("Reference Restaurant Timber",new Color(.13f,.10f,.07f));var wall=M("Reference Restaurant Walls",new Color(.58f,.22f,.13f));var red=M("Reference Roof Red",new Color(.27f,.095f,.07f));var orange=M("Reference Paper Lantern",new Color(1,.37f,.055f));var yellow=M("Reference Sign Gold",new Color(1,.71f,.10f));var cream=M("Reference Lantern Cream",new Color(.98f,.85f,.47f));
        float x=90,z=112;
        P("Restaurant Coastal Sand",PrimitiveType.Sphere,new Vector3(x+2,-.2f,z+8),new Vector3(36,1,28),M("Reference Beach",new Color(.79f,.72f,.45f)),parent);
        P("Timber Waterfront Deck",PrimitiveType.Cube,new Vector3(x,.5f,z),new Vector3(29,.65f,12),timber,parent);
        for(int i=0;i<13;i++)P("Deck Plank",PrimitiveType.Cube,new Vector3(x-13+i*2.2f,.88f,z-1),new Vector3(2.04f,.08f,10),M("Reference Deck Planks",new Color(.46f,.30f,.15f)),parent);
        for(int i=0;i<12;i++)P("Dock Pile",PrimitiveType.Cylinder,new Vector3(x-13+i*2.35f,-.2f,z-5.4f),new Vector3(.6f,1.2f,.6f),timber,parent);
        P("Open Dining House",PrimitiveType.Cube,new Vector3(x,4,z+2),new Vector3(23,6,7),wall,parent);
        P("Dark Dining Opening",PrimitiveType.Cube,new Vector3(x,3.7f,z-1.57f),new Vector3(21,4.4f,.18f),timber,parent);
        for(int i=0;i<6;i++)P("Front Timber Post",PrimitiveType.Cube,new Vector3(x-10+i*4,3.7f,z-2),new Vector3(.4f,5.8f,.4f),timber,parent);
        P("Long Sushi Counter",PrimitiveType.Cube,new Vector3(x,2.5f,z-2.5f),new Vector3(22,1.1f,1.5f),M("Reference Counter",new Color(.54f,.35f,.16f)),parent);
        var front=P("Sloped Roof Front",PrimitiveType.Cube,new Vector3(x,7.4f,z),new Vector3(27,.45f,7),red,parent);front.transform.rotation=Quaternion.Euler(-16,0,0);
        var back=P("Sloped Roof Back",PrimitiveType.Cube,new Vector3(x,7.4f,z+6),new Vector3(27,.45f,7),red,parent);back.transform.rotation=Quaternion.Euler(16,0,0);
        Rod("Roof Ridge",new Vector3(x-14,8.5f,z+3),new Vector3(x+14,8.5f,z+3),.3f,red,parent);
        for(int i=0;i<8;i++)
        {
            var lp=new Vector3(x-11+i*3.15f,5.6f,z-3.2f);Rod("Lantern Cord",lp+Vector3.up*.8f,lp,.035f,timber,parent);
            P("Paper Lantern",PrimitiveType.Sphere,lp,new Vector3(1.1f,1.6f,1.1f),i%2==0?orange:cream,parent);
            P("Lantern End",PrimitiveType.Cylinder,lp+Vector3.down*.8f,new Vector3(.6f,.1f,.6f),timber,parent);
        }
        P("Restaurant Sign Board",PrimitiveType.Cube,new Vector3(x,9.4f,z+1),new Vector3(15,2.4f,.4f),timber,parent);
        var sign=new GameObject("World Restaurant Sign");sign.transform.SetParent(parent);sign.transform.position=new Vector3(x,9.4f,z+.74f);
        var text=sign.AddComponent<TextMesh>();text.text="SUSHI";text.fontSize=90;text.characterSize=.22f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(1,.68f,.12f);
        for(int i=0;i<6;i++){P("Dining Stool",PrimitiveType.Cylinder,new Vector3(x-9+i*3.5f,1.6f,z-3.4f),new Vector3(1,.6f,1),timber,parent);}
        var distant=new GameObject("Distant Shore Hamlet").transform;distant.SetParent(decor);
        P("Far Shore Island",PrimitiveType.Sphere,new Vector3(20,.1f,270),new Vector3(39,1.2f,16),M("Reference Beach",new Color(.79f,.72f,.45f)),distant);
        for(int i=0;i<5;i++){var p=new Vector3(7+i*6,2.7f,270);P("Far Shore Cabin",PrimitiveType.Cube,p,new Vector3(4.7f,4.8f,4),M("Reference Cabin",new Color(.82f,.66f,.35f)),distant);P("Far Shore Roof",PrimitiveType.Cube,p+Vector3.up*2.8f,new Vector3(6,.6f,5.4f),red,distant);}
    }
    static float Range(float a,float b){return a+(float)rng.NextDouble()*(b-a);}
    static Bounds B(GameObject o){var rr=o.GetComponentsInChildren<Renderer>();if(rr.Length==0)throw new Exception("No mesh: "+o.name);var b=rr[0].bounds;foreach(var r in rr)b.Encapsulate(r.bounds);return b;}
    static void Bottom(GameObject o,Vector3 p){var b=B(o);o.transform.position+=p-new Vector3(b.center.x,b.min.y,b.center.z);}
    static Material M(string name,Color color){string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.18f);return m;}
    static GameObject P(string name,PrimitiveType type,Vector3 pos,Vector3 scale,Material mat,Transform parent){var o=GameObject.CreatePrimitive(type);o.name=name;o.transform.SetParent(parent);o.transform.position=pos;o.transform.localScale=scale;o.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());return o;}
    static void Rod(string name,Vector3 a,Vector3 b,float radius,Material mat,Transform parent){var o=P(name,PrimitiveType.Cylinder,(a+b)*.5f,new Vector3(radius,(b-a).magnitude*.5f,radius),mat,parent);o.transform.up=(b-a).normalized;}
    static GameObject Prefab(string name,Vector3 pos,Vector3 size,Transform parent,bool uniform)
    {
        string path=null;foreach(var guid in AssetDatabase.FindAssets(name+" t:Prefab",new[]{"Assets/99.StoreAssets/Map"})){var p=AssetDatabase.GUIDToAssetPath(guid);if(Path.GetFileNameWithoutExtension(p)==name){path=p;break;}}
        if(path==null)throw new Exception("Missing map asset "+name);
        var holder=new GameObject("Map Foliage — "+name);holder.transform.SetParent(parent);var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));model.transform.SetParent(holder.transform,false);model.transform.localRotation=Quaternion.Euler(0,Range(0,360),0);
        foreach(var lod in model.GetComponentsInChildren<LODGroup>()){var a=lod.GetLODs();if(a.Length>0&&a[0].renderers.Length>0)lod.ForceLOD(0);else lod.enabled=false;}
        var bb=B(holder);var s=new Vector3(size.x/Mathf.Max(bb.size.x,.01f),size.y/Mathf.Max(bb.size.y,.01f),size.z/Mathf.Max(bb.size.z,.01f));holder.transform.localScale=uniform?Vector3.one*Mathf.Min(s.x,s.y,s.z):s;Bottom(holder,pos);return holder;
    }
    static void Preview(Camera camera){var rt=new RenderTexture(1440,810,24);rt.Create();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1440,810,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,810),0,0);image.Apply();File.WriteAllBytes("Library/BoatSurfacePreview/BoatTest.png",image.EncodeToPNG());RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
}
