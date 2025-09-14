using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;

public class TerrainGeneration : MonoBehaviour
{
    public int RandomSeed;
    public int Width;
    public int Depth;
    public int MaxHeight;
    public Material TerrainMaterial;
    public float Frequency = 1.0f;
    public float Amplitude = 0.5f;
    public float Lacunarity = 2.0f;
    public float Gain = 0.5f;
    public int Octaves = 8;
    public float Scale = 0.01f;
    public float NormalizeBias = 1.0f;

    private GameObject[] mRealTerrains = new GameObject[3];
    private NoiseAlgorithm mTerrainNoise;
    private GameObject mLight;

    // code to get rid of fog from: https://forum.unity.com/threads/how-do-i-turn-off-fog-on-a-specific-camera-using-urp.1373826/
    // Unity calls this method automatically when it enables this component
    private void OnEnable()
    {
        // Add WriteLogMessage as a delegate of the RenderPipelineManager.beginCameraRendering event
        RenderPipelineManager.beginCameraRendering += BeginRender;
        RenderPipelineManager.endCameraRendering += EndRender;
    }
 
    // Unity calls this method automatically when it disables this component
    private void OnDisable()
    {
        // Remove WriteLogMessage as a delegate of the  RenderPipelineManager.beginCameraRendering event
        RenderPipelineManager.beginCameraRendering -= BeginRender;
        RenderPipelineManager.endCameraRendering -= EndRender;
    }
 
    // When this method is a delegate of RenderPipeline.beginCameraRendering event, Unity calls this method every time it raises the beginCameraRendering event
    void BeginRender(ScriptableRenderContext context, Camera camera)
    {
        if(camera.name == "Main Camera No Fog")
        {
            //Debug.Log("Turn fog off");
            RenderSettings.fog = false;
        }
         
    }
 
    void EndRender(ScriptableRenderContext context, Camera camera)
    {
        if (camera.name == "Main Camera No Fog")
        {
            //Debug.Log("Turn fog on");
            RenderSettings.fog = true;
        }
    }
    
    // Start is called before the first frame update
    void Start()
    {
        // create a height map using perlin noise and fractal brownian motion
        mTerrainNoise = new NoiseAlgorithm();
        mTerrainNoise.InitializeNoise(Width + 1, Depth + 1, RandomSeed);
        mTerrainNoise.InitializePerlinNoise(Frequency, Amplitude, Octaves, 
            Lacunarity, Gain, Scale, NormalizeBias);
        NativeArray<float> terrainHeightMap = new NativeArray<float>((Width+1) * (Depth+1), Allocator.Persistent);
        mTerrainNoise.setNoise(terrainHeightMap, 0, 0);

        // create the mesh and set it to the terrain variable

        for (int i = 0; i < 3; i++)
        {
            mRealTerrains[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mRealTerrains[i].transform.position = new Vector3(0, 0, i * 128);
            MeshRenderer meshRenderer = mRealTerrains[i].GetComponent<MeshRenderer>();
            MeshFilter meshFilter = mRealTerrains[i].GetComponent<MeshFilter>();
            meshRenderer.material = TerrainMaterial;
            meshFilter.mesh = GenerateTerrainMesh(terrainHeightMap);

        }
        terrainHeightMap.Dispose();
        NoiseAlgorithm.OnExit();
    }

    private void Update()
    {
      
    }

    // create a new mesh with
    // perlin noise
    // makes a quad and connects it with the next quad
    // uses whatever texture the material is given
    public Mesh GenerateTerrainMesh(NativeArray<float> heightMap)
    {
        int width = Width + 1, depth = Depth + 1;
        int height = MaxHeight;
        int indicesIndex = 0;
        int vertexIndex = 0;
        int vertexMultiplier = 4; // create quads to fit uv's to so we can use more than one uv (4 vertices to a quad)

        Mesh terrainMesh = new Mesh();
        List<Vector3> vert = new List<Vector3>(width * depth * vertexMultiplier);
        List<int> indices = new List<int>(width * depth * 6);
        List<Vector2> uvs = new List<Vector2>(width * depth);
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                if (x < width - 1 && z < depth - 1)
                {
                    // note: since perlin goes up to 1.0 multiplying by a height will tend to set
                    // the average around maxheight/2. We remove most of that extra by subtracting maxheight/2
                    // so our ground isn't always way up in the air
                    float y = heightMap[(x) * (width) + (z)] * height - (MaxHeight/2.0f);
                    float useAltXPlusY = heightMap[(x + 1) * (width) + (z)] * height - (MaxHeight/2.0f);
                    float useAltZPlusY = heightMap[(x) * (width) + (z + 1)] * height- (MaxHeight/2.0f);
                    float useAltXAndZPlusY = heightMap[(x + 1) * (width) + (z + 1)] * height- (MaxHeight/2.0f);
                    
                    vert.Add(new float3(x, y, z));
                    vert.Add(new float3(x, useAltZPlusY, z + 1)); 
                    vert.Add(new float3(x + 1, useAltXPlusY, z));  
                    vert.Add(new float3(x + 1, useAltXAndZPlusY, z + 1));

                    // add uv's
                    // remember to give it all 4 sides of the image coords
                    /*                    uvs.Add(new Vector2(0.0f,0.0f));
                                        uvs.Add(new Vector2(0.0f,0.5f));
                                        uvs.Add(new Vector2(0.5f, 0.0f));
                                        uvs.Add(new Vector2(0.5f, 0.5f));*/

                    int tileIndex;

                    if (y < MaxHeight / 10) tileIndex = 1;
                    else if (y > MaxHeight / 10 && y < MaxHeight / 8) tileIndex = 3;
                    else if (y > MaxHeight / 8 && y < MaxHeight / 6) tileIndex = 5;
                    else tileIndex = 7;

                    int atlasSize = 4;
                    float tileSize = 1.0f / atlasSize;

                    int tileX = tileIndex % atlasSize;
                    int tileY = atlasSize - 1 - (tileIndex / atlasSize); //flip Y axis

                    float uMin = tileX * tileSize;
                    float vMin = tileY * tileSize;
                    float uMax = uMin + tileSize;
                    float vMax = vMin + tileSize;

                    uvs.Add(new Vector2(uMin, vMin));
                    uvs.Add(new Vector2(uMin, vMax));
                    uvs.Add(new Vector2(uMax, vMin));
                    uvs.Add(new Vector2(uMax, vMax));


                    // front or top face indices for a quad
                    //0,2,1,0,3,2
                    indices.Add(vertexIndex);
                    indices.Add(vertexIndex + 1);
                    indices.Add(vertexIndex + 2);
                    indices.Add(vertexIndex + 3);
                    indices.Add(vertexIndex + 2);
                    indices.Add(vertexIndex + 1);
                    indicesIndex += 6;
                    vertexIndex += vertexMultiplier;
                }
            }

        }
        
        // set the terrain var's for the mesh
        terrainMesh.vertices = vert.ToArray();
        terrainMesh.triangles = indices.ToArray();
        terrainMesh.SetUVs(0, uvs);
        
        // reset the mesh
        terrainMesh.RecalculateNormals();
        terrainMesh.RecalculateBounds();

        return terrainMesh;
    }

}
