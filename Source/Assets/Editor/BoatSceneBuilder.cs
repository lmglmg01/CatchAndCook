using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class BoatSceneBuilder
{
    const string Output = "Assets/03.Resources/BoatSurface";
    const string ScenePath = "Assets/01.Scenes/BoatTest.unity";
    const string Marker = "Library/BoatSurface.request";
    static Transform root;
    static Material rock, foliage, sand, wood, cream, roof;
    static System.Random random;
    static BoatSceneBuilder() { EditorApplication.delayCall += AutoBuild; }
    static void AutoBuild()
    {
        if (!File.Exists(Marker)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += AutoBuild; return; }
        string request = File.ReadAllText(Marker).Trim(); File.Delete(Marker);
        try { if(request == "backdrop") AdjustBackdrop(); else if(request == "map-cliffs") ReplaceMapCliffs(); else if(request == "larger-cliffs") EnlargeMapCliffs(); else if(request == "widen-cliffs") WidenCliffs(); else Build(); } catch (Exception e) { File.WriteAllText("Library/BoatSurface.result.txt", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/C Team/Build Boat Surface Scene")]
    public static void Build()
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save the open scene before building BoatTest; unsaved work was preserved.");
        Directory.CreateDirectory(Output);
        Directory.CreateDirectory("Library/BoatSurfaceBackup");
        if (File.Exists(ScenePath)) File.Copy(ScenePath, "Library/BoatSurfaceBackup/BoatTest-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        root = new GameObject("Boat Surface — Tropical Lagoon").transform;
        random = new System.Random(1942);
        rock = Mat("Limestone", new Color(.39f,.65f,.69f), .12f);
        foliage = Mat("Island Green", new Color(.39f,.78f,.16f), .05f);
        sand = Mat("Shore Sand", new Color(.91f,.81f,.52f), .12f);
        wood = Mat("Pier Wood", new Color(.34f,.23f,.14f), .08f);
        cream = Mat("Coastal Walls", new Color(.95f,.85f,.63f), .12f);
        roof = Mat("Terracotta Roof", new Color(.56f,.18f,.14f), .12f);

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.42f,.58f,.64f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(.18f,.76f,.82f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 95;
        RenderSettings.fogEndDistance = 220;
        var sun = new GameObject("Afternoon Sun").AddComponent<Light>();
        sun.transform.SetParent(root); sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(38,-35,0);
        sun.color = new Color(1,.96f,.8f); sun.intensity = 1.05f; sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;

        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera"; camera.transform.position = new Vector3(0,10,-62);
        camera.transform.rotation = Quaternion.Euler(-3,0,0);
        camera.orthographic = false; camera.fieldOfView = 42; camera.orthographicSize = 21;
        camera.nearClipPlane = .3f; camera.farClipPlane = 350;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f,.8f,.86f);
        camera.allowHDR = true; camera.AdditionalData().renderPostProcessing = false;
        camera.gameObject.AddComponent<AudioListener>();
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Output+"/Afternoon Grade.asset");
        if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Output+"/Afternoon Grade.asset");}
        if(!profile.TryGet<Tonemapping>(out var tone))tone=profile.Add<Tonemapping>();
        tone.mode.Override(TonemappingMode.Neutral);
        var volume=new GameObject("Optional Neutral Color Grade").AddComponent<Volume>();volume.transform.SetParent(root);volume.isGlobal=true;volume.sharedProfile=profile;volume.enabled=false;

        var skyMat = ShaderMat("Tropical Sky", "BoatSurface/TropicalSky");
        var sky = Primitive("Sky and Soft Clouds", PrimitiveType.Quad, new Vector3(0,62,190), new Vector3(400,200,1), skyMat);
        sky.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var water = Primitive("Turquoise Ocean", PrimitiveType.Plane, new Vector3(0,-.35f,65), new Vector3(28,1,28), ShaderMat("Lagoon Water", "BoatSurface/LagoonWater"));
        water.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        var islands = new GameObject("Limestone Islands and Palms").transform; islands.SetParent(root);
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 10; i++)
            {
                float z = 29 + i * 8.4f;
                float x = side * (44 - i * 2.2f + Range(-2,2));
                float height = Mathf.Lerp(30,5,i/9f) + Range(-2,3);
                float width = Range(8,13);
                var cliff = Asset("P_CubicCliff" + ((i%5)+1).ToString("00"), new Vector3(x,-1.4f,z), new Vector3(width,height,12), Range(-22,22), islands);
                Paint(cliff, rock);
                Primitive("Green Limestone Ledge", PrimitiveType.Sphere, new Vector3(x,height-1.7f,z), new Vector3(width*1.12f,1.8f,10),foliage,islands);
                for (int p=0;p<3;p++)
                {
                    var palm = Asset("P_CoconutPalm" + ((p%2)+1).ToString("00"), new Vector3(x+Range(-width*.33f,width*.33f),height-.8f,z+Range(-3,3)), Vector3.one*Range(3,5), Range(0,360), islands, true);
                    ConvertMaterials(palm);
                }
                if(i<5) Primitive("Lower Green Shelf", PrimitiveType.Sphere,new Vector3(x-side*width*.24f,height*.55f,z-4), new Vector3(width*.7f,1.1f,5),foliage,islands);
                Primitive("Shoreline",PrimitiveType.Sphere,new Vector3(x,-.15f,z),new Vector3(width*1.3f,.7f,15),sand,islands);
            }
        }
        // Small, distant restaurant and dock on the right shore.
        var village = new GameObject("Distant Coastal Restaurant").transform; village.SetParent(root);
        Primitive("Restaurant Pier",PrimitiveType.Cube,new Vector3(30,.35f,44),new Vector3(15,.45f,7),wood,village);
        for(int i=0;i<7;i++) Primitive("Pier Piling",PrimitiveType.Cylinder,new Vector3(23+i*2.2f,-.5f,40.8f),new Vector3(.32f,1.4f,.32f),wood,village);
        Primitive("Restaurant",PrimitiveType.Cube,new Vector3(31,3,46),new Vector3(9,5,4),cream,village);
        Primitive("Broad Roof",PrimitiveType.Cube,new Vector3(31,5.8f,46),new Vector3(11,.6f,6),roof,village);
        for(int i=0;i<5;i++) Primitive("Open Windows",PrimitiveType.Cube,new Vector3(27.4f+i*1.8f,3,43.94f),new Vector3(1.1f,2.4f,.13f),wood,village);
        for(int i=0;i<5;i++) Primitive("Warm Lantern",PrimitiveType.Sphere,new Vector3(27.4f+i*1.8f,4.6f,42.8f),new Vector3(.45f,.65f,.45f),Mat("Lantern",new Color(1,.58f,.14f),0),village);
        Primitive("Distant Village Island",PrimitiveType.Sphere,new Vector3(9,-.15f,116),new Vector3(26,1,12),sand,village);
        Primitive("Distant Village Green",PrimitiveType.Sphere,new Vector3(9,.45f,119),new Vector3(24,1.5f,10),foliage,village);
        for(int i=0;i<4;i++)
        {
            float x=3+i*4;
            Primitive("Distant Coastal Hut",PrimitiveType.Cube,new Vector3(x,1.4f,116),new Vector3(3,2.4f,3),cream,village);
            Primitive("Distant Hut Roof",PrimitiveType.Cube,new Vector3(x,2.8f,116),new Vector3(3.8f,.5f,3.8f),roof,village);
        }

        var boatAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/99.StoreAssets/Boat/Meshy_AI_Low_Poly_Fishing_Boat_1011110927_texture_fbx/Meshy_AI_Low_Poly_Fishing_Boat_1011110927_texture.fbx");
        if (!boatAsset) throw new FileNotFoundException("Fishing boat model missing");
        var boat = new GameObject("Fishing Boat — Foreground"); boat.transform.SetParent(root);
        var boatModel = (GameObject)PrefabUtility.InstantiatePrefab(boatAsset);
        boatModel.transform.SetParent(boat.transform,false);
        boatModel.transform.localRotation = Quaternion.Euler(-90,0,0);
        foreach(var lod in boat.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
        Normalize(boat,new Vector3(24,9,9),false);
        boat.transform.rotation = Quaternion.Euler(0,180,0);
        PlaceBottom(boat,new Vector3(-17,-.18f,-1));
        var boatMat = Mat("Fishing Boat Painted", Color.white,.25f);
        boatMat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/99.StoreAssets/Boat/Meshy_AI_Low_Poly_Fishing_Boat_1011110927_texture_fbx/Meshy_AI_Low_Poly_Fishing_Boat_1011110927_texture.png"));
        Paint(boat,boatMat);
        File.WriteAllText("Library/BoatSurface.boat.txt", "Boat bounds: " + BoundsOf(boat) + "; scale: " + boat.transform.localScale + "; position: " + boat.transform.position + "; renderers: " + boat.GetComponentsInChildren<Renderer>().Length);
        foreach(var r in boat.GetComponentsInChildren<Renderer>()) { r.enabled=true; r.forceRenderingOff=false; }
        var deckDetails = new GameObject("Yellow Dive Canopy and Deck Equipment").transform; deckDetails.SetParent(root);
        var canopy=Mat("Dive Canopy Yellow",new Color(1,.64f,.035f),.16f);
        var frame=Mat("Canopy Frame",new Color(.72f,.88f,.88f),.35f);
        Primitive("Yellow Canvas Canopy",PrimitiveType.Cube,new Vector3(-10,6.5f,-.8f),new Vector3(8,.3f,6.5f),canopy,deckDetails);
        for(int ix=0;ix<2;ix++)for(int iz=0;iz<2;iz++)
            Primitive("Canopy Support",PrimitiveType.Cylinder,new Vector3(-13.7f+ix*7.4f,4.55f,-3.7f+iz*5.8f),new Vector3(.13f,1.9f,.13f),frame,deckDetails);
        var tanks=Mat("Dive Tanks",new Color(.92f,.65f,.10f),.3f);
        // Wake marks emphasize the foreground waterline, without any screen UI.
        var foam = Mat("Sea Foam",new Color(.65f,.96f,.95f),.2f);
        for(int i=0;i<17;i++) Primitive("Boat Wake",PrimitiveType.Sphere,new Vector3(-29+i*1.4f,-.19f,-6+Range(-.6f,.6f)),new Vector3(Range(.6f,1.4f),.07f,.4f),foam);

        SetBackdrop();
        ReplaceCliffObjects();
        EditorSceneManager.SaveScene(scene,ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = boat;
        if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(new Vector3(0,9,35),camera.transform.rotation,65);
        RenderPreview(camera);
        File.WriteAllText("Library/BoatSurface.result.txt","SUCCESS: BoatTest saved. UI canvas count: " + UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length + "; renderer count: " + UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length);
        Debug.Log("Boat surface scene created: " + ScenePath);
    }
    static UniversalAdditionalCameraData AdditionalData(this Camera camera) { return camera.GetUniversalAdditionalCameraData(); }
    static float Range(float a,float b) { return a+(float)random.NextDouble()*(b-a); }
    static Material Mat(string name,Color color,float smooth)
    {
        string path=Output+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m) { m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);return m;
    }
    static Material ShaderMat(string name,string shader) {
        var m=AssetDatabase.LoadAssetAtPath<Material>(Output+"/"+name+".mat");
        if(!m){ var s=Shader.Find(shader);if(!s)throw new Exception("Missing shader: "+shader);m=new Material(s);AssetDatabase.CreateAsset(m,Output+"/"+name+".mat"); }return m;
    }
    static GameObject Primitive(string name,PrimitiveType type,Vector3 pos,Vector3 scale,Material material,Transform parent=null)
    { var o=GameObject.CreatePrimitive(type);o.name=name;o.transform.SetParent(parent?parent:root);o.transform.position=pos;o.transform.localScale=scale;o.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());return o; }
    static Bounds BoundsOf(GameObject o) { var rs=o.GetComponentsInChildren<Renderer>();if(rs.Length==0)throw new Exception("No mesh: "+o.name);var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b; }
    static void Normalize(GameObject o,Vector3 size,bool uniform)
    { var b=BoundsOf(o);var s=new Vector3(size.x/Mathf.Max(b.size.x,.01f),size.y/Mathf.Max(b.size.y,.01f),size.z/Mathf.Max(b.size.z,.01f));o.transform.localScale=Vector3.Scale(o.transform.localScale,uniform?Vector3.one*Mathf.Min(s.x,s.y,s.z):s); }
    static void PlaceBottom(GameObject o,Vector3 pos){var b=BoundsOf(o);o.transform.position+=pos-new Vector3(b.center.x,b.min.y,b.center.z);}
    static GameObject Asset(string name,Vector3 pos,Vector3 size,float angle,Transform parent,bool uniform=false)
    {
        string[] guids=AssetDatabase.FindAssets(name+" t:Prefab",new[]{"Assets/99.StoreAssets/Map"});string path=null;
        foreach(var g in guids){var p=AssetDatabase.GUIDToAssetPath(g);if(Path.GetFileNameWithoutExtension(p)==name){path=p;break;}}
        if(path==null)throw new Exception("Asset missing: "+name);
        var o=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));o.transform.SetParent(parent);o.transform.rotation=Quaternion.Euler(0,angle,0);Normalize(o,size,uniform);PlaceBottom(o,pos);return o;
    }
    static void Paint(GameObject o,Material material){foreach(var r in o.GetComponentsInChildren<Renderer>()){var a=r.sharedMaterials;for(int i=0;i<a.Length;i++)a[i]=material;r.sharedMaterials=a;}}
    static void ConvertMaterials(GameObject o)
    {
        foreach(var r in o.GetComponentsInChildren<Renderer>()){
            var a=r.sharedMaterials;for(int i=0;i<a.Length;i++){
                var old=a[i];if(!old)continue;
                var m=Mat("Palm "+old.name,old.name.Contains("Bark")?new Color(.48f,.33f,.19f):new Color(.29f,.65f,.12f),.08f);
                Texture tex=old.HasProperty("_BaseMap")?old.GetTexture("_BaseMap"):old.HasProperty("_MainTex")?old.GetTexture("_MainTex"):null;
                if(tex)m.SetTexture("_BaseMap",tex);a[i]=m;
            }r.sharedMaterials=a;
        }
    }
    [MenuItem("Tools/C Team/Move Cliffs Into Background")]
    public static void AdjustBackdrop()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!=ScenePath)throw new InvalidOperationException("Open BoatTest before adjusting the cliffs.");
        Directory.CreateDirectory("Library/BoatSurfaceBackup");
        File.Copy(ScenePath,"Library/BoatSurfaceBackup/BoatTest-before-cliff-adjust-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
        SetBackdrop();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        RenderPreview(Camera.main);
        File.WriteAllText("Library/BoatSurface.result.txt","SUCCESS: Cliffs scaled to 1.6 and moved 40 units farther back. Boat and camera preserved.");
    }
    static void SetBackdrop()
    {
        var islands=GameObject.Find("Limestone Islands and Palms");
        if(!islands)throw new InvalidOperationException("Island group missing.");
        Undo.RecordObject(islands.transform,"Enlarge and move cliffs");
        islands.transform.localScale=Vector3.one*1.6f;
        islands.transform.position=new Vector3(0,0,40);
        var village=GameObject.Find("Distant Coastal Restaurant");
        if(village){Undo.RecordObject(village.transform,"Align coast with cliffs");village.transform.localScale=Vector3.one*1.6f;village.transform.position=new Vector3(0,0,40);}
        var sky=GameObject.Find("Sky and Soft Clouds");
        if(sky){sky.transform.position=new Vector3(0,92,270);sky.transform.localScale=new Vector3(620,310,1);}
        var ocean=GameObject.Find("Turquoise Ocean");
        if(ocean){ocean.transform.position=new Vector3(0,-.35f,100);ocean.transform.localScale=new Vector3(40,1,40);}
        RenderSettings.fogStartDistance=145;
        RenderSettings.fogEndDistance=330;
    }
    [MenuItem("Tools/C Team/Use Original Map Cliff Assets")]
    public static void ReplaceMapCliffs()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!=ScenePath)throw new InvalidOperationException("Open BoatTest first.");
        Directory.CreateDirectory("Library/BoatSurfaceBackup");
        File.Copy(ScenePath,"Library/BoatSurfaceBackup/BoatTest-before-map-cliffs-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
        ReplaceCliffObjects();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        RenderPreview(Camera.main);
        File.WriteAllText("Library/BoatSurface.result.txt","SUCCESS: Map Classic cliff and shelf prefabs installed with original materials. Background scale and placement preserved.");
    }
    static void ReplaceCliffObjects()
    {
        var group=GameObject.Find("Limestone Islands and Palms").transform;
        var children=new System.Collections.Generic.List<Transform>();foreach(Transform child in group)children.Add(child);
        int cliffIndex=0,shelfIndex=0;
        foreach(var child in children)
        {
            bool cliff=child.name.StartsWith("P_CubicCliff");
            bool shelf=child.name=="Green Limestone Ledge" || child.name=="Lower Green Shelf";
            if(!cliff&&!shelf)continue;
            var bounds=BoundsOf(child.gameObject);
            string assetName=cliff?"P_CliffClassic"+((cliffIndex++%6)+1):"P_ShelfClassic"+((shelfIndex++%7)+1);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/99.StoreAssets/Map/Environment/Rocks/Classic/Prefabs/"+assetName+".prefab");
            if(!prefab)throw new Exception("Missing map cliff prefab: "+assetName);
            var holder=new GameObject(cliff?"Map Cliff — "+assetName:"Map Rock Shelf — "+assetName);
            holder.transform.SetParent(group,false);holder.transform.rotation=Quaternion.identity;
            var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab);model.transform.SetParent(holder.transform,false);
            foreach(var lod in model.GetComponentsInChildren<LODGroup>())
            {var levels=lod.GetLODs();if(levels.Length>0&&levels[0].renderers.Length>0)lod.ForceLOD(0);else lod.enabled=false;}
            Normalize(holder,bounds.size,false);
            PlaceBottom(holder,new Vector3(bounds.center.x,bounds.min.y,bounds.center.z));
            Undo.RegisterCreatedObjectUndo(holder,"Use original map cliffs");
            Undo.DestroyObjectImmediate(child.gameObject);
        }
    }
    public static void EnlargeMapCliffs()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!=ScenePath)throw new InvalidOperationException("Open BoatTest first.");
        var group=GameObject.Find("Limestone Islands and Palms").transform;
        var cliffs=new System.Collections.Generic.List<GameObject>();
        var oldBounds=new System.Collections.Generic.List<Bounds>();
        foreach(Transform t in group)if(t.name.StartsWith("Map Cliff")){cliffs.Add(t.gameObject);oldBounds.Add(BoundsOf(t.gameObject));}
        if(cliffs.Count==0)throw new InvalidOperationException("Map cliff objects missing.");
        Directory.CreateDirectory("Library/BoatSurfaceBackup");
        File.Copy(ScenePath,"Library/BoatSurfaceBackup/BoatTest-before-enlarge-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
        foreach(Transform t in group)
        {
            if(!t.name.StartsWith("Map Rock Shelf")&&!t.name.StartsWith("P_CoconutPalm"))continue;
            var b=BoundsOf(t.gameObject);int nearest=0;float distance=float.MaxValue;
            for(int i=0;i<oldBounds.Count;i++){float d=Vector2.Distance(new Vector2(b.center.x,b.center.z),new Vector2(oldBounds[i].center.x,oldBounds[i].center.z));if(d<distance){distance=d;nearest=i;}}
            var cb=oldBounds[nearest];Undo.RecordObject(t,"Enlarge cliff vegetation and ledges");t.localScale*=1.5f;
            var anchor=new Vector3(cb.center.x,cb.min.y,cb.center.z);
            var oldBottom=new Vector3(b.center.x,b.min.y,b.center.z);
            PlaceBottom(t.gameObject,anchor+(oldBottom-anchor)*1.5f);
        }
        for(int i=0;i<cliffs.Count;i++)
        {
            var o=cliffs[i];var b=oldBounds[i];Undo.RecordObject(o.transform,"Enlarge map cliffs 1.5x");o.transform.localScale*=1.5f;
            PlaceBottom(o,new Vector3(b.center.x,b.min.y,b.center.z));
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        RenderPreview(Camera.main);
        File.WriteAllText("Library/BoatSurface.result.txt","SUCCESS: Existing map cliffs enlarged by 1.5x, keeping shoreline anchors and background distance. Shelves and palms aligned.");
    }
    public static void WidenCliffs()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!=ScenePath)throw new InvalidOperationException("Open BoatTest first.");
        var islands=GameObject.Find("Limestone Islands and Palms");
        if(!islands)throw new InvalidOperationException("Island group missing.");
        Directory.CreateDirectory("Library/BoatSurfaceBackup");
        File.Copy(ScenePath,"Library/BoatSurfaceBackup/BoatTest-before-widen-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
        foreach(Transform child in islands.transform)
        {
            float x=BoundsOf(child.gameObject).center.x;
            Undo.RecordObject(child,"Move cliffs outward slightly");
            child.position+=new Vector3(x<0?-5f:5f,0,0);
        }
        var village=GameObject.Find("Distant Coastal Restaurant");
        if(village){foreach(Transform child in village.transform){if(BoundsOf(child.gameObject).center.z<180){Undo.RecordObject(child,"Align shore with right cliffs");child.position+=new Vector3(5,0,0);}}}
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        RenderPreview(Camera.main);
        File.WriteAllText("Library/BoatSurface.result.txt","SUCCESS: Left cliffs shifted 5 units left, right cliffs 5 units right. Palms and shores aligned; enlarged size preserved.");
    }
    static void RenderPreview(Camera camera)
    {
        var rt=new RenderTexture(1440,810,24);rt.Create();
        var request=new UniversalRenderPipeline.SingleCameraRequest{destination=rt};
        RenderPipeline.SubmitRenderRequest(camera,request);
        var previous=RenderTexture.active;RenderTexture.active=rt;
        var image=new Texture2D(1440,810,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,810),0,0);image.Apply();
        Directory.CreateDirectory("Library/BoatSurfacePreview");File.WriteAllBytes("Library/BoatSurfacePreview/BoatTest.png",image.EncodeToPNG());
        RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
    }
}
