using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Editor-only authoring. No stage generation or new behaviour runs during a battle.
public static class RuinsStageBuilder
{
    public const string Folder = "Assets/Sekkei/RuinsStageRefined";
    public const string ScenePath = "Assets/Scenes/RuinsStageRefined.unity";
    private const float SceneScale = .45f;
    private const string Report = "Temp/RuinsStageBuild.txt";
    private static System.Random random;
    private static Transform root;
    private static Material stone, floor, darkStone, moss, rock, foliage, grass, bush, leaf, inlay;
    private static Vector3 origin;
    private static int meshNumber;

    [MenuItem("Tools/Stage/Build Refined Ruins")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        var source = SceneManager.GetActiveScene();
        if(source.path == "Assets/Scenes/RuinsStagePreview.unity" || source.path == ScenePath)
        {
            if(source.isDirty) throw new InvalidOperationException("Save your preview changes first.");
            source=EditorSceneManager.OpenScene("Assets/TitleScene.unity");
        }
        if (source.path != "Assets/TitleScene.unity") throw new InvalidOperationException("Open TitleScene first.");
        if (source.isDirty) throw new InvalidOperationException("Save your scene changes first. No changes were made.");
        if (GameObject.Find("Ruins Stage")) throw new InvalidOperationException("Stage already exists; rebuild in the original scene.");
        // Work in a separate copy until the actual render is reviewed.
        Directory.CreateDirectory(Folder);
        File.Copy(source.path, "Temp/TitleScene.before-ruins.unity", true);
        EditorSceneManager.SaveScene(source, ScenePath, true);
        EditorSceneManager.OpenScene(ScenePath);
        var gameplay = Object.FindObjectsOfType<MonoBehaviour>(true)
            .Where(c => c != null && c.GetType().Assembly.GetName().Name == "Assembly-CSharp")
            .ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
        var spawn = GameObject.Find("EnemySpawnPoint").transform;
        Vector3 spawnPosition = spawn.position;
        origin = new Vector3(spawn.position.x, spawn.position.y, spawn.position.z - 8);
        random = new System.Random(240921);
        meshNumber = 0;
        root = new GameObject("Ruins Stage").transform;
        root.position = origin;
        // Preserve colliders, spawn anchors and all game objects. Only hide old scenery renderers.
        var oldEnvironment = GameObject.Find("Environment");
        foreach (var renderer in oldEnvironment.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        foreach (var terrain in oldEnvironment.GetComponentsInChildren<Terrain>(true)) terrain.enabled = false;

        stone = WeatheredMaterial("Weathered limestone",new Color(.69f,.65f,.53f),.23f);
        floor = WeatheredMaterial("Ancient paving",new Color(.66f,.63f,.52f),.30f);
        darkStone = WeatheredMaterial("Fractured stone",new Color(.47f,.46f,.38f),.32f);
        inlay = WeatheredMaterial("Carved recesses",new Color(.25f,.28f,.15f),.55f);
        moss = Material("Moss in joints", "Assets/textures/forrest_ground_01_diff_4k.jpg", new Color(.39f, .45f, .24f));
        rock = Material("Mossy boulders", "Assets/model/rock_moss_set_01_4k.blend/textures/rock_moss_set_01_diff_4k.jpg", Color.white);
        foliage = Material("Shrub foliage", "Assets/model/shrub_02_4k.blend/textures/shrub_02_diff_4k.jpg", new Color(.78f,.87f,.59f), true);
        grass = Material("Weeds", "Assets/model/weed_plant_02_4k.blend/textures/weed_plant_02_diff_4k.jpg", new Color(.82f,.88f,.62f), true);
        bush = Material("Rooibos ground cover", "Assets/model/wild_rooibos_bush_4k.blend/textures/wild_rooibos_bush_diff_4k.jpg", new Color(.85f,.92f,.68f), true);
        leaf = Material("Ivy leaves", null,new Color(.42f,.48f,.15f));leaf.SetFloat("_Cull",0);
        leaf.EnableKeyword("_EMISSION");leaf.SetColor("_EmissionColor",new Color(.014f,.022f,.003f));
        TerrainStage();
        Arena();
        Architecture();
        Scatter();
        Lighting();
        ScaleScenery(spawnPosition,oldEnvironment.transform.Find("Stage").GetComponent<Renderer>().bounds.max.y);
        Camera camera = Camera.main;
        camera.transform.position = origin + new Vector3(0, 4.0f, -21)*SceneScale;
        camera.transform.rotation = Quaternion.Euler(3, 0, 0);
        camera.fieldOfView = 50;
        camera.farClipPlane = 700;
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;
        camera.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        if (spawn.position != spawnPosition || gameplay.Any(pair => pair.Key == null || EditorJsonUtility.ToJson(pair.Key) != pair.Value))
            throw new InvalidOperationException("Gameplay preservation check failed. Original scene is unchanged.");
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Capture(camera, "StageReview/ruins-refined.png", 1600, 900);
        var previewEnemy=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/testEnemy.prefab"));
        previewEnemy.name="Temporary render-only enemy";previewEnemy.transform.position=spawnPosition;
        var previewAnimation=previewEnemy.GetComponent<EnemyAnimationPlayer>();
        if(previewAnimation != null && previewAnimation.animations != null)
            previewAnimation.animations.idle.SampleAnimation(previewAnimation.animator.gameObject,0.2f);
        Capture(camera,"StageReview/ruins-refined-with-enemy.png",1600,900);
        Object.DestroyImmediate(previewEnemy);
        Vector3 cameraPosition = camera.transform.position;
        Quaternion cameraRotation = camera.transform.rotation;
        camera.transform.position = origin + new Vector3(0, 62, 0)*SceneScale;
        camera.transform.rotation = Quaternion.Euler(90, 0, 0);
        Capture(camera, "StageReview/ruins-refined-top.png", 1200, 1200);
        camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllText(Report, $"PASS: {gameplay.Count} gameplay components unchanged; enemy spawn unchanged.\nScene: {ScenePath}\nOriginal TitleScene unchanged.\nRenderers: {root.GetComponentsInChildren<Renderer>().Length}\nBlueprint dimensions uniformly adapted by {SceneScale} for the existing character size.\n");
    }

    private static Material Material(string name, string texture, Color color, bool cutout = false)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
        if(texture!=null) mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", .12f);
        if (cutout)
        {
            // These source JPGs have separate opacity maps; without this leaves become opaque cards.
            string opacityPath=texture.Replace("_diff_4k.jpg","_alpha_4k.png");
            var opacity=AssetDatabase.LoadAssetAtPath<Texture2D>(opacityPath);
            if(opacity == null) throw new InvalidOperationException("Missing foliage opacity map: " + opacityPath);
            var basePixels=ReadTexture(mat.GetTexture("_BaseMap"));
            var alphaPixels=ReadTexture(opacity);
            var pixels=basePixels.GetPixels32();var alpha=alphaPixels.GetPixels32();
            for(int i=0;i<pixels.Length;i++) pixels[i].a=alpha[i].r;
            basePixels.SetPixels32(pixels);basePixels.Apply();
            string combinedPath=Folder+"/"+name+" RGBA.png";
            File.WriteAllBytes(combinedPath,basePixels.EncodeToPNG());
            Object.DestroyImmediate(basePixels);Object.DestroyImmediate(alphaPixels);
            AssetDatabase.ImportAsset(combinedPath);
            var importer=(TextureImporter)AssetImporter.GetAtPath(combinedPath);
            importer.alphaIsTransparency=true;importer.SaveAndReimport();
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(combinedPath));
            mat.SetFloat("_AlphaClip", 1); mat.SetFloat("_Cutoff", .24f); mat.SetFloat("_Cull", 0);
            mat.EnableKeyword("_ALPHATEST_ON"); mat.SetOverrideTag("RenderType", "TransparentCutout"); mat.renderQueue = 2450;
        }
        AssetDatabase.CreateAsset(mat, Folder + "/" + name + ".mat");
        return mat;
    }

    private static Material WeatheredMaterial(string name,Color tint,float coverage)
    {
        string normal=Folder+"/Limestone normal.exr";
        if(!File.Exists(normal))
        {
            File.Copy("Assets/textures/cracked_concrete_nor_gl_4k.exr",normal);
            AssetDatabase.ImportAsset(normal);
            var importer=(TextureImporter)AssetImporter.GetAtPath(normal);
            importer.textureType=TextureImporterType.NormalMap;importer.sRGBTexture=false;
            importer.maxTextureSize=2048;importer.SaveAndReimport();
        }
        var mat=new Material(Shader.Find("Ruins/Weathered Stone")){name=name,enableInstancing=true};
        mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/textures/cracked_concrete_diff_4k.jpg"));
        mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
        mat.SetTexture("_MossMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/textures/forrest_ground_01_diff_4k.jpg"));
        mat.SetColor("_BaseColor",tint);mat.SetColor("_MossColor",new Color(.28f,.34f,.09f));
        mat.SetFloat("_TextureScale",1.2f);mat.SetFloat("_MossAmount",coverage);mat.SetFloat("_BumpScale",.6f);
        AssetDatabase.CreateAsset(mat,Folder+"/"+name+".mat");return mat;
    }

    private static void ScaleScenery(Vector3 spawn,float surfaceY)
    {
        // Terrain does not support transform scaling: scale its data and offset explicitly.
        foreach(Transform child in root)
        {
            var terrains=child.GetComponentsInChildren<Terrain>();
            if(terrains.Length>0)
            {
                foreach(var terrain in terrains)
                {
                    terrain.terrainData.size*=SceneScale;
                    terrain.transform.localPosition*=SceneScale;
                    EditorUtility.SetDirty(terrain.terrainData);
                }
            }
            else {child.localPosition*=SceneScale;child.localScale*=SceneScale;}
        }
        origin=new Vector3(spawn.x,surfaceY,spawn.z-8*SceneScale);root.position=origin;
        RenderSettings.fogDensity/=(SceneScale);
    }

    private static Texture2D ReadTexture(Texture source)
    {
        var rt=RenderTexture.GetTemporary(1024,1024,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var previous=RenderTexture.active;
        try
        {
            Graphics.Blit(source,rt);RenderTexture.active=rt;
            var result=new Texture2D(1024,1024,TextureFormat.RGBA32,false);
            result.ReadPixels(new Rect(0,0,1024,1024),0,0);result.Apply();return result;
        }
        finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
    }

    private static Transform Group(string name, Transform parent = null)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent == null ? root : parent, false);
        return t;
    }

    private static float R(float min, float max) => min + (float)random.NextDouble() * (max - min);

    private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material mat, Vector3 angles = default)
    {
        if(type==PrimitiveType.Cube && Mathf.Min(scale.x,scale.y,scale.z)>.18f)
        {
            var block=MeshObject(name,parent,ChippedBlock(scale),mat);
            block.transform.localPosition=position;block.transform.localEulerAngles=angles;return block;
        }
        var go = GameObject.CreatePrimitive(type);
        go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = scale; go.transform.localEulerAngles = angles;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic);
        return go;
    }

    private static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat)
    {
        mesh.RecalculateNormals(); mesh.RecalculateBounds();mesh.RecalculateTangents();
        mesh.name = name;
        AssetDatabase.CreateAsset(mesh, Folder + "/Geometry" + meshNumber++ + ".asset");
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic);
        return go;
    }

    private static Mesh ChippedBlock(Vector3 size)
    {
        Vector3 h=size*.5f;float bevel=Mathf.Min(h.x,h.y,h.z)*R(.18f,.4f);
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
        float seed=R(0,100);
        Func<Vector3,Vector3> worn=p=>p+new Vector3(
            Mathf.PerlinNoise(p.y*3+seed,p.z*3)-.5f,
            Mathf.PerlinNoise(p.z*3+seed,p.x*3)-.5f,
            Mathf.PerlinNoise(p.x*3+seed,p.y*3)-.5f)*bevel*.6f;
        Func<int,int,int,int,Vector3> corner=(x,y,z,axis)=>new Vector3(x*(h.x-(axis==0?0:bevel)),y*(h.y-(axis==1?0:bevel)),z*(h.z-(axis==2?0:bevel)));
        Action<Vector3[]> face=points=>
        {
            Vector3 center=Vector3.zero;foreach(var p in points)center+=p;
            if(Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),center)<0)Array.Reverse(points);
            int start=vertices.Count;
            foreach(var p in points){vertices.Add(worn(p));uv.Add(new Vector2(p.x+p.z,p.y+p.z));}
            for(int j=1;j<points.Length-1;j++)tris.AddRange(new[]{start,start+j,start+j+1});
        };
        for(int s=-1;s<=1;s+=2)
        {
            face(new[]{corner(s,-1,-1,0),corner(s,1,-1,0),corner(s,1,1,0),corner(s,-1,1,0)});
            face(new[]{corner(-1,s,-1,1),corner(1,s,-1,1),corner(1,s,1,1),corner(-1,s,1,1)});
            face(new[]{corner(-1,-1,s,2),corner(1,-1,s,2),corner(1,1,s,2),corner(-1,1,s,2)});
            for(int t=-1;t<=1;t+=2)
            {
                face(new[]{corner(s,t,-1,0),corner(s,t,1,0),corner(s,t,1,1),corner(s,t,-1,1)});
                face(new[]{corner(s,-1,t,0),corner(s,1,t,0),corner(s,1,t,2),corner(s,-1,t,2)});
                face(new[]{corner(-1,s,t,1),corner(1,s,t,1),corner(1,s,t,2),corner(-1,s,t,2)});
                for(int u=-1;u<=1;u+=2)face(new[]{corner(s,t,u,0),corner(s,t,u,1),corner(s,t,u,2)});
            }
        }
        var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);return mesh;
    }

    // Discrete wedge-shaped stones with real gaps, thickness and world-space texture coordinates.
    private static Mesh Wedge(float inner, float outer, float a, float b, float bottom, float top, int segments = 6)
    {
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
        Action<Vector3, Vector3, Vector3, Vector3> quad = (p, q, r, s) =>
        {
            int i = vertices.Count; vertices.AddRange(new[] { p,q,r,s });
            Vector3 normal=Vector3.Cross(q-p,r-p).normalized;
            foreach(var point in new[]{p,q,r,s})
                uv.Add(Mathf.Abs(normal.y)>.5f ? new Vector2(point.x,point.z)/2 : new Vector2(point.x+point.z,point.y)/2);
            triangles.AddRange(new[] { i,i+1,i+2,i,i+2,i+3 });
        };
        float seed=R(0,100);
        Func<float,float,float,Vector3> pnt = (radius, angle, height) =>
        {
            float noise=(Mathf.PerlinNoise(angle*11+seed,radius*3)-.5f)*.045f;
            return new Vector3(Mathf.Cos(angle)*(radius+noise),height,Mathf.Sin(angle)*(radius+noise));
        };
        for (int j=0;j<segments;j++)
        {
            float t = Mathf.Lerp(a,b,(float)j/segments), u = Mathf.Lerp(a,b,(float)(j+1)/segments);
            quad(pnt(inner,t,top),pnt(inner,u,top),pnt(outer,u,top),pnt(outer,t,top));
            quad(pnt(outer,t,bottom),pnt(outer,t,top),pnt(outer,u,top),pnt(outer,u,bottom));
            quad(pnt(inner,u,bottom),pnt(inner,u,top),pnt(inner,t,top),pnt(inner,t,bottom));
        }
        quad(pnt(inner,a,bottom),pnt(inner,a,top),pnt(outer,a,top),pnt(outer,a,bottom));
        quad(pnt(outer,b,bottom),pnt(outer,b,top),pnt(inner,b,top),pnt(inner,b,bottom));
        var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0); return mesh;
    }

    private static void Arena()
    {
        var parent = Group("01 Circular sanctuary - diameter 20m");
        Primitive("Moss between paving", PrimitiveType.Cylinder, parent, new Vector3(0,-.17f,0),new Vector3(20,.14f,20),moss);
        float[] radii = { .06f, 1.15f, 1.40f, 2.65f, 2.92f, 4.15f, 4.45f, 5.75f, 6.03f,7.30f,7.65f,9.12f,9.43f,10f };
        for (int ring=0; ring<radii.Length-1; ring++)
        {
            int count = Mathf.CeilToInt(radii[ring+1]*6);
            bool trim = ring%2==1;
            var pieces = new List<CombineInstance>();
            for (int j=0;j<count;j++)
            {
                float a = (j+.3f*(ring%2))*Mathf.PI*2/count;
                float b = a + Mathf.PI*2/count;
                Mesh mesh = Wedge(radii[ring]+.035f,radii[ring+1]-.035f,a+.006f,b-.006f,-.20f,R(-.018f,.018f),4);
                pieces.Add(new CombineInstance { mesh=mesh, transform=Matrix4x4.identity });
            }
            var combined = new Mesh(); combined.CombineMeshes(pieces.ToArray());
            foreach(var c in pieces) Object.DestroyImmediate(c.mesh);
            MeshObject("Paving ring " + ring, parent, combined, trim ? stone : floor);
        }
        FloorCarvings(parent);
    }

    private static void FloorCarvings(Transform parent)
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
        foreach(float radius in new[]{1.27f,2.78f,4.30f,5.89f,7.47f,9.27f})
        {
            var ring=new List<Vector3>();
            for(int j=0;j<=256;j++)
            {
                float a=j*Mathf.PI*2/256;
                float r=radius+Mathf.Sin(a*32)*.055f;
                ring.Add(new Vector3(Mathf.Sin(a)*r,.039f,Mathf.Cos(a)*r));
            }
            AppendStroke(v,uv,indices,ring,.025f);
        }
        for(int petal=0;petal<8;petal++)
        {
            var path=new List<Vector3>();float a=petal*Mathf.PI/4;
            for(int j=0;j<=40;j++)
            {
                float t=j*Mathf.PI*2/40;
                Vector3 p=new Vector3(Mathf.Sin(t)*.31f,.04f,.52f+Mathf.Cos(t)*.44f);
                path.Add(Quaternion.Euler(0,a*Mathf.Rad2Deg,0)*p);
            }
            AppendStroke(v,uv,indices,path,.04f);
        }
        // Broken Greek-key motifs on the broad outer band.
        for(int j=0;j<64;j++)
        {
            if(j%11==0)continue;
            float a=j*Mathf.PI*2/64;
            var path=new List<Vector3>();
            foreach(var p in new[]{new Vector2(0,-.13f),new Vector2(.07f,-.13f),new Vector2(.07f,.14f),new Vector2(.035f,.14f),new Vector2(.035f,-.01f)})
                path.Add(new Vector3(Mathf.Sin(a+p.x)*(9.65f+p.y),.039f,Mathf.Cos(a+p.x)*(9.65f+p.y)));
            AppendStroke(v,uv,indices,path,.032f);
        }
        for(int crack=0;crack<28;crack++)
        {
            float a=R(0,Mathf.PI*2),r=R(2,9);var path=new List<Vector3>();
            for(int j=0;j<5;j++)path.Add(new Vector3(Mathf.Sin(a)*r+R(-.08f,.08f),.038f,Mathf.Cos(a)*r+j*.16f));
            AppendStroke(v,uv,indices,path,R(.012f,.027f));
        }
        var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);
        MeshObject("Weathered carved rosette and meanders",parent,mesh,inlay);
    }

    private static void AppendStroke(List<Vector3> v,List<Vector2> uv,List<int> tris,List<Vector3> path,float width)
    {
        for(int j=0;j<path.Count-1;j++)
        {
            Vector3 p=path[j],q=path[j+1],d=q-p,n=new Vector3(-d.z,0,d.x).normalized*width*.5f;
            int k=v.Count;v.AddRange(new[]{p+n,p-n,q-n,q+n});
            uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});
            tris.AddRange(new[]{k,k+2,k+1,k,k+3,k+2});
        }
    }

    private static Mesh FlutedShaft(float height, float radius, bool broken=false)
    {
        const int sides=128; const int rows=48;
        var v=new List<Vector3>(); var uv=new List<Vector2>(); var tris=new List<int>();
        for(int row=0;row<=rows;row++) for(int i=0;i<=sides;i++)
        {
            float a=i*Mathf.PI*2/sides, y=(float)row/rows;
            float seam=Mathf.Abs(Mathf.Repeat(height*y+.03f,1.6f)-.03f);
            float r=radius*(1-.13f*y)*(1-.095f*(1+Mathf.Cos(a*16)));
            r-=Mathf.Clamp01(1-seam/.08f)*.02f;
            r+=(Mathf.PerlinNoise(a*4+radius*17,y*13)-.5f)*.045f;
            float fracture=Mathf.Cos(a+.5f)*.32f+Mathf.Sin(a*5)*.12f;
            float h=height*y+(broken ? Mathf.Pow(y,12)*fracture : Mathf.Pow(y,24)*fracture*.12f);
            v.Add(new Vector3(Mathf.Cos(a)*r,h,Mathf.Sin(a)*r)); uv.Add(new Vector2(a*radius/2,h/2));
            if(row<rows && i<sides) {int n=row*(sides+1)+i; tris.AddRange(new[]{n,n+sides+1,n+1,n+1,n+sides+1,n+sides+2});}
        }
        int cap=v.Count; v.Add(new Vector3(0,height-.05f,0)); uv.Add(Vector2.zero);
        for(int i=0;i<sides;i++) tris.AddRange(new[]{cap,rows*(sides+1)+i+1,rows*(sides+1)+i});
        var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tris,0);return m;
    }

    private static Transform Column(Transform parent, Vector3 position, float height, float radius, bool broken)
    {
        var c=Group(broken ? "Broken fluted column" : "Fluted column",parent); c.localPosition=position;
        Primitive("Lower plinth",PrimitiveType.Cube,c,new Vector3(0,.2f,0),new Vector3(radius*3,.4f,radius*3),darkStone);
        Primitive("Upper plinth",PrimitiveType.Cube,c,new Vector3(0,.5f,0),new Vector3(radius*2.7f,.22f,radius*2.7f),stone);
        Primitive("Torus base",PrimitiveType.Cylinder,c,new Vector3(0,.77f,0),new Vector3(radius*2.35f,.16f,radius*2.35f),stone);
        var shaft=MeshObject("Weathered flutes",c,FlutedShaft(height-1.55f,radius,broken),stone);shaft.transform.localPosition=Vector3.up*.9f;
        if(!broken)
        {
            Primitive("Capital neck",PrimitiveType.Cylinder,c,new Vector3(0,height-.55f,0),new Vector3(radius*2.15f,.14f,radius*2.15f),stone);
            Primitive("Capital echinus",PrimitiveType.Cube,c,new Vector3(0,height-.3f,0),new Vector3(radius*2.6f,.28f,radius*2.6f),stone);
            Primitive("Capital abacus",PrimitiveType.Cube,c,new Vector3(0,height-.05f,0),new Vector3(radius*3,.22f,radius*3),stone);
            for(int ornament=0;ornament<12;ornament++)
            {
                float a=ornament*Mathf.PI/6;
                Primitive("Eroded capital ornament",PrimitiveType.Cube,c,new Vector3(Mathf.Sin(a)*radius*.94f,height-.62f,Mathf.Cos(a)*radius*.94f),new Vector3(.20f,.47f,.24f),stone,new Vector3(0,ornament*30,12));
            }
        }
        Ivy(c,radius,broken ? Mathf.Min(height-1,4) : height-.4f);
        return c;
    }

    private static void Ivy(Transform parent,float radius,float height)
    {
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        for(int vine=0;vine<4;vine++)
        {
            float angle=R(0,Mathf.PI*2);
            float reach=height*R(.5f,1);
            for(float y=.25f;y<reach;y+=R(.14f,.25f))
            {
                if(R(0,1)<.15f)continue;
                float a=angle+Mathf.Sin(y*.8f)*.4f+y*.14f;
                Vector3 outward=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));
                Vector3 p=outward*(radius*(1-.13f*y/Mathf.Max(height,1))+.035f)+Vector3.up*y;
                for(int side=-1;side<=1;side+=2)
                {
                    Vector3 tangent=new Vector3(outward.z,0,-outward.x)*side;
                    Vector3 direction=(tangent*.8f+Vector3.down*R(.2f,.65f)).normalized;
                    AddLeaf(vertices,uv,triangles,p+outward*.03f,direction,outward,R(.12f,.25f));
                }
            }
        }
        var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
        MeshObject("Clinging ivy",parent,mesh,leaf);
    }

    private static void AddLeaf(List<Vector3> v,List<Vector2> uv,List<int> tris,Vector3 start,Vector3 direction,Vector3 facing,float length)
    {
        Vector3 side=Vector3.Cross(direction,facing).normalized;
        Vector2[] outline={new Vector2(0,0),new Vector2(-.31f,.24f),new Vector2(-.46f,.64f),new Vector2(-.17f,.58f),new Vector2(0,1),new Vector2(.17f,.58f),new Vector2(.46f,.64f),new Vector2(.31f,.24f)};
        int k=v.Count;v.Add(start+direction*length*.44f+facing*.027f);uv.Add(new Vector2(.5f,.44f));
        foreach(var p in outline){v.Add(start+(side*p.x+direction*p.y)*length);uv.Add(new Vector2(p.x+.5f,p.y));}
        for(int j=0;j<outline.Length;j++)tris.AddRange(new[]{k,k+1+j,k+1+(j+1)%outline.Length});
    }

    private static void Architecture()
    {
        var architecture=Group("02 Ruined colonnade and northern arch");
        Column(architecture,new Vector3(-11,0,-5),14,.86f,false).localRotation=Quaternion.Euler(0,0,-1.3f);
        Column(architecture,new Vector3(11.8f,0,-3),12.5f,.82f,false).localRotation=Quaternion.Euler(.8f,0,1.2f);
        Column(architecture,new Vector3(-12,0,5),8,.8f,true);
        Column(architecture,new Vector3(12,0,7),10.5f,.84f,false);
        Column(architecture,new Vector3(-9.5f,0,12.5f),10.8f,.76f,false);
        Column(architecture,new Vector3(9,0,14),6.5f,.8f,true);
        Column(architecture,new Vector3(-16,0,14),5.2f,.83f,true);
        Column(architecture,new Vector3(16.5f,0,18),8.8f,.85f,true);
        Column(architecture,new Vector3(-14,0,-15),6,.86f,true);
        Column(architecture,new Vector3(11.9f,0,2),8,.82f,true);
        var arch=Group("Northern triumphal arch - height 15m",architecture);arch.localPosition=new Vector3(0,0,19);
        for(int side=-1;side<=1;side+=2)
        {
            Column(arch,new Vector3(side*3.8f,0,0),10.5f,.88f,false);
            Column(arch,new Vector3(side*3.8f,0,2),10.5f,.88f,false);
            Primitive("Pier core",PrimitiveType.Cube,arch,new Vector3(side*3.8f,5,1),new Vector3(1.8f,10,2.1f),darkStone);
        }
        // Wedges rotate into the vertical plane to form a real opening.
        for(int i=0;i<15;i++)
        {
            float a=i*Mathf.PI/15+.009f,b=(i+1)*Mathf.PI/15-.009f;
            var wedge=MeshObject("Arch voussoir " + i,arch,Wedge(2.9f,4.1f,a,b,-1,1,4),stone);
            wedge.transform.localPosition=new Vector3(0,9.7f,1);
            wedge.transform.localRotation=Quaternion.Euler(-90,0,0);
        }
        Primitive("Entablature",PrimitiveType.Cube,arch,new Vector3(0,14,1),new Vector3(9.5f,.6f,3),darkStone);
        Primitive("Cornice",PrimitiveType.Cube,arch,new Vector3(0,14.45f,1),new Vector3(10,.28f,3.3f),stone);
        for(int i=0;i<14;i++)
            Primitive("Cornice dentil",PrimitiveType.Cube,arch,new Vector3(-4.55f+i*.7f,13.7f,-.61f),new Vector3(.35f,.3f,.26f),stone);
        for(int i=0;i<12;i++)
            Primitive("Broken attic masonry",PrimitiveType.Cube,arch,new Vector3(R(-3.8f,3.8f),14.7f,R(.1f,1.8f)),new Vector3(R(.6f,1.4f),R(.3f,.8f),R(.7f,1.5f)),stone,new Vector3(R(-6,6),R(-12,12),R(-6,6)));
        for(int course=0;course<3;course++)for(int j=0;j<5-course;j++)
            Primitive("Fractured pediment",PrimitiveType.Cube,arch,new Vector3(-2.6f+j*1.05f+course*.35f,14.9f+course*.44f,1.2f),new Vector3(R(.8f,1.2f),.48f,1.5f),stone,new Vector3(R(-5,5),R(-10,10),R(-8,8)));
        // Low fragments never cross the open battle area.
        for(int side=-1;side<=1;side+=2) for(int row=0;row<3;row++) for(int i=0;i<7-row;i++)
            Primitive("Collapsed boundary wall",PrimitiveType.Cube,architecture,new Vector3(side*(17+i*.22f),.4f+row*.76f,2+i*1.3f+row*.35f),new Vector3(1.4f,.72f,1.24f),stone,new Vector3(0,R(-5,5),R(-3,3)));
    }

    private static void TerrainStage()
    {
        var parent=Group("00 Terrain - 60m plateau and distant mountains");
        var layers=new TerrainLayer[3];
        string[] tex={"Assets/textures/forrest_ground_01_diff_4k.jpg","Assets/textures/cracked_concrete_diff_4k.jpg","Assets/textures/coral_stone_wall_diff_4k.jpg"};
        for(int i=0;i<3;i++)
        {
            layers[i]=new TerrainLayer { diffuseTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(tex[i]),tileSize=new Vector2(i==0?5:8,i==0?5:8),smoothness=.05f };
            AssetDatabase.CreateAsset(layers[i],Folder+"/TerrainLayer"+i+".terrainlayer");
        }
        var material=new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
        AssetDatabase.CreateAsset(material,Folder+"/Terrain.mat");
        CreateTerrain("Sanctuary plateau 60 x 60",parent,60,12,257,new Vector3(-30,-3,-30),layers,material,false);
        CreateTerrain("Distant mountain range",parent,640,140,1025,new Vector3(-320,-48,-320),layers,material,true);
    }

    private static void CreateTerrain(string name,Transform parent,float size,float height,int resolution,Vector3 position,TerrainLayer[] layers,Material mat,bool mountains)
    {
        var data=new TerrainData { heightmapResolution=resolution,size=new Vector3(size,height,size),alphamapResolution=128,terrainLayers=layers };
        var h=new float[resolution,resolution];
        for(int z=0;z<resolution;z++) for(int x=0;x<resolution;x++)
        {
            float wx=position.x+size*x/(resolution-1),wz=position.z+size*z/(resolution-1);
            float r=Mathf.Sqrt(wx*wx+wz*wz);
            if(!mountains)
            {
                float noise=Mathf.PerlinNoise(wx*.14f+27,wz*.14f+62)*.9f+Mathf.PerlinNoise(wx*.4f+20,wz*.4f+20)*.2f;
                float edge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(22,31,Mathf.Max(Mathf.Abs(wx),Mathf.Abs(wz))));
                h[z,x]=(2.65f+noise*Mathf.SmoothStep(0,1,Mathf.InverseLerp(9,16,r))-edge*2.4f)/height;
            }
            else
            {
                float n=0,amp=1,freq=.010f;
                for(int o=0;o<6;o++){ n+=(1-Mathf.Abs(2*Mathf.PerlinNoise(wx*freq+13,wz*freq+54)-1))*amp;freq*=2.12f;amp*=.48f; }
                float envelope=Mathf.SmoothStep(0,1,Mathf.InverseLerp(65,175,r));
                float valley=Mathf.Exp(-Mathf.Pow((wx-25)/60,2))*.25f;
                h[z,x]=(12+envelope*(20+62*Mathf.Pow(n/1.8f,2.1f))*(1-valley))/height;
            }
        }
        data.SetHeights(0,0,h);
        var map=new float[128,128,3];
        for(int z=0;z<128;z++) for(int x=0;x<128;x++)
        {
            float steep=data.GetSteepness(x/127f,z/127f)/60;
            float noise=Mathf.PerlinNoise(x*.08f+2,z*.08f+3);
            float stoneWeight=mountains?Mathf.Clamp01(.3f+steep):Mathf.Clamp01(steep*.5f);
            map[z,x,0]=(1-stoneWeight)*(mountains?.2f:.7f+noise*.25f);
            map[z,x,1]=1-stoneWeight-map[z,x,0];map[z,x,2]=stoneWeight;
        }
        data.SetAlphamaps(0,0,map);
        AssetDatabase.CreateAsset(data,Folder+"/"+name+".asset");
        var go=Terrain.CreateTerrainGameObject(data);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;
        Object.DestroyImmediate(go.GetComponent<TerrainCollider>());
        var terrain=go.GetComponent<Terrain>();terrain.materialTemplate=mat;terrain.drawInstanced=true;terrain.heightmapPixelError=4;
    }

    private static void Scatter()
    {
        var parent=Group("03 Rubble, moss and vegetation");
        var rocks=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/model/rock_moss_set_01_4k.blend/rock_moss_set_01_4k.blend").GetComponentsInChildren<MeshFilter>();
        var shrubs=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/model/shrub_02_4k.blend/shrub_02_4k.blend").GetComponentsInChildren<MeshFilter>();
        var weeds=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/model/weed_plant_02_4k.blend/weed_plant_02_4k.blend").GetComponentsInChildren<MeshFilter>();
        var bushes=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/model/wild_rooibos_bush_4k.blend/wild_rooibos_bush_4k.blend").GetComponentsInChildren<MeshFilter>();
        for(int i=0;i<100;i++)
        {
            float a=R(0,Mathf.PI*2),r=R(11,26);
            Vector3 p=new Vector3(Mathf.Sin(a)*r,0,Mathf.Cos(a)*r);p.y=Ground(p)-.10f;
            ModelPiece("Moss covered boulder",parent,rocks[random.Next(rocks.Length)],p,R(.25f,1.2f),rock);
        }
        for(int i=0;i<130;i++)
        {
            float a=R(0,Mathf.PI*2),r=R(10.4f,25);
            Primitive("Fallen masonry",PrimitiveType.Cube,parent,new Vector3(Mathf.Sin(a)*r,R(0,.4f),Mathf.Cos(a)*r),new Vector3(R(.25f,1.3f),R(.2f,.85f),R(.3f,1.1f)),i%3==0?darkStone:stone,new Vector3(R(-22,22),R(0,360),R(-20,20)));
        }
        for(int i=0;i<140;i++)
        {
            float a=R(0,Mathf.PI*2),r=R(10.2f,27);
            var p=new Vector3(Mathf.Sin(a)*r,0,Mathf.Cos(a)*r);p.y=Ground(p);
            ModelPiece("Shrub",parent,shrubs[random.Next(shrubs.Length)],p,R(.65f,1.6f),foliage);
        }
        for(int i=0;i<480;i++)
        {
            float a=R(0,Mathf.PI*2),r=R(10.2f,25);
            var p=new Vector3(Mathf.Sin(a)*r,0,Mathf.Cos(a)*r);p.y=Ground(p);
            ModelPiece("Low spreading ground cover",parent,bushes[random.Next(bushes.Length)],p,R(.7f,1.8f),bush);
        }
        for(int i=0;i<300;i++)
        {
            float a=R(0,Mathf.PI*2),r=i<120?new[]{1.83f,3.68f,5.62f,7.52f,9.98f}[random.Next(5)]:R(10,23);
            var p=new Vector3(Mathf.Sin(a)*r,.008f,Mathf.Cos(a)*r);if(r>10)p.y=Ground(p);
            ModelPiece("Weeds in joints",parent,weeds[random.Next(weeds.Length)],p,R(.6f,1.8f),grass);
        }
        GrassTufts(parent);
        foreach(var column in root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Fluted column").ToArray())
        {
            float top=column.GetComponentsInChildren<MeshRenderer>().Max(r=>column.InverseTransformPoint(r.bounds.max).y);
            ModelPiece("Growth on capital",column,bushes[4],new Vector3(R(-.4f,.4f),top-.04f,R(-.4f,.4f)),R(.8f,1.2f),bush);
        }
    }

    private static float Ground(Vector3 p)
    {
        var terrain=root.GetComponentInChildren<Terrain>();
        return terrain.SampleHeight(root.TransformPoint(p))+terrain.transform.position.y-root.position.y;
    }

    private static void GrassTufts(Transform parent)
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
        for(int tuft=0;tuft<1600;tuft++)
        {
            float a=R(0,Mathf.PI*2),r=R(10.1f,26);Vector3 p=new Vector3(Mathf.Sin(a)*r,0,Mathf.Cos(a)*r);p.y=Ground(p);
            for(int blade=0;blade<7;blade++)
            {
                Vector3 start=p+new Vector3(R(-.2f,.2f),0,R(-.2f,.2f));
                float angle=R(0,Mathf.PI*2),height=R(.10f,.32f);
                Vector3 side=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*R(.012f,.028f);
                int k=v.Count;v.AddRange(new[]{start-side,start+side,start+Vector3.up*height+side*R(1,4)});
                uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.up});tris.AddRange(new[]{k,k+1,k+2});
            }
        }
        var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);MeshObject("Fine grass tufts",parent,mesh,leaf);
    }

    private static void ModelPiece(string name,Transform parent,MeshFilter source,Vector3 position,float scale,Material mat)
    {
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
        go.GetComponent<MeshFilter>().sharedMesh=source.sharedMesh;go.GetComponent<Renderer>().sharedMaterial=mat;
        GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic);
        // Blender source mesh is Z-up; preserve the imported model's accumulated orientation.
        go.transform.localRotation=Quaternion.Euler(0,R(0,360),0)*source.transform.rotation;
        go.transform.localScale=Vector3.one*scale;go.transform.localPosition=position;
        var bounds=go.GetComponent<Renderer>().bounds;
        go.transform.position+=Vector3.up*(parent.TransformPoint(position).y-bounds.min.y);
    }

    private static void Lighting()
    {
        var sun=GameObject.Find("Directional Light").GetComponent<Light>();
        sun.transform.rotation=Quaternion.Euler(18,-155,0);
        sun.useColorTemperature=false;sun.color=new Color(1,.79f,.53f);sun.intensity=2.5f;
        sun.shadows=LightShadows.Soft;sun.shadowBias=.025f;sun.shadowNormalBias=.2f;
        var fill=Group("Soft sky fill").gameObject.AddComponent<Light>();
        fill.type=LightType.Directional;fill.color=new Color(.80f,.85f,1f);fill.intensity=1.2f;
        fill.transform.rotation=Quaternion.Euler(38,-25,0);fill.shadows=LightShadows.None;
        var sky=new Material(Shader.Find("Skybox/Panoramic"));
        sky.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture>("Assets/HDRI/citrus_orchard_road_puresky_4k.exr"));
        sky.SetFloat("_Exposure",.45f);sky.SetFloat("_Rotation",107);sky.SetColor("_Tint",new Color(.69f,.57f,.48f));
        AssetDatabase.CreateAsset(sky,Folder+"/Evening sky.mat");RenderSettings.skybox=sky;
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.36f,.41f,.48f);
        RenderSettings.ambientEquatorColor=new Color(.32f,.30f,.24f);
        RenderSettings.ambientGroundColor=new Color(.13f,.15f,.12f);
        var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.30f,.33f,.37f));RenderSettings.ambientProbe=ambient;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
        RenderSettings.fogColor=new Color(.45f,.51f,.59f);RenderSettings.fogDensity=.0042f;
        var volume=Group("04 Evening grade").gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=10;
        var profile=ScriptableObject.CreateInstance<VolumeProfile>();
        var tonemap=profile.Add<Tonemapping>();tonemap.mode.Override(TonemappingMode.ACES);
        var bloom=profile.Add<Bloom>();bloom.intensity.Override(.2f);bloom.threshold.Override(1.1f);
        var color=profile.Add<ColorAdjustments>();color.postExposure.Override(.2f);color.contrast.Override(8);color.saturation.Override(2);
        AssetDatabase.CreateAsset(profile,Folder+"/Evening grade.asset");
        foreach(var component in profile.components) AssetDatabase.AddObjectToAsset(component,profile);
        volume.sharedProfile=profile;
        var mistMaterial=new Material(Shader.Find("Ruins/Valley Mist"));
        mistMaterial.SetColor("_BaseColor",new Color(.72f,.74f,.75f,.24f));
        AssetDatabase.CreateAsset(mistMaterial,Folder+"/Valley mist.mat");
        var mist=Group("05 Low mist in the distant valleys");
        for(int row=0;row<4;row++)for(int cloud=0;cloud<5;cloud++)
        {
            var go=Primitive("Valley mist veil",PrimitiveType.Quad,mist,new Vector3(-110+cloud*55+R(-12,12),-5+row*2.3f,48+row*35+R(-6,6)),new Vector3(110,14+row*4,1),mistMaterial);
            go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
    }

    public static void Capture(Camera camera,string path,int width,int height)
    {
        var target=new RenderTexture(width,height,24);var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
        var previous=camera.targetTexture;var active=RenderTexture.active;
        bool async=ShaderUtil.allowAsyncCompilation;
        try {ShaderUtil.allowAsyncCompilation=false;camera.targetTexture=target;camera.Render();camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
        finally {ShaderUtil.allowAsyncCompilation=async;camera.targetTexture=previous;RenderTexture.active=active;Object.DestroyImmediate(texture);Object.DestroyImmediate(target);}
    }
}
