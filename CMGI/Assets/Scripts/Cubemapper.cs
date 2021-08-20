using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class Cubemapper : MonoBehaviour
{
    private Transform[] probePositions;
    public Transform probeHolder;
    public Transform volume;
    private const float maxDistance = 5.0f;
    public float bias = .05f;
    public float giFactor = 1.5f;
    public LayerMask camLayerMask;

    public Material[] GIMats;

    [Header("Probes")]
    public Vector3Int probeResolution;
    public Vector3 margin;
    public GameObject probe;
    public Transform probeLocator;
    public Material selectedMaterial;
    public GameObject textMesh;
    public GameObject canvas;
    public bool useCameraDepthTarget = true;
    public bool realTimeRendering = false;
    public bool showProbes = false;

    private Vector3 probeSpacing;
    private Bounds probeBounds;
    private List<Vector4> probeVectorPositions;
    private Vector3Int[] directions = new Vector3Int[] { new Vector3Int(-1, -1, 0), new Vector3Int(0, -1, 0), new Vector3Int(-1, -1, -1), new Vector3Int(0, -1, -1),
                                                         new Vector3Int(-1, 0, 0), new Vector3Int(0, 0, 0), new Vector3Int(-1, 0, -1), new Vector3Int(0, 0, -1) };
    private Camera tmp_camera;
    private RenderTexture textureRGB;
    private RenderTexture textureCubeArray;
    private RenderTexture textureCubeArrayDepth;
    private RenderTexture textureDepth;

    private Shader diffuseDepthShader;

    private List<Color> probeColors;

    private const int RGBD_WH = 64;
    private const int DEPTH_BITS = 16;

    private int count = 0;


    private Transform[] InitializeProbes()
    {
        Bounds volumeBounds = volume.GetComponent<Renderer>().bounds;
        volumeBounds.min += margin;
        volumeBounds.max -= margin;

        probeBounds = volumeBounds;

        float width = volumeBounds.max.x - volumeBounds.min.x;
        float xProbeSpacing = width / (float)(probeResolution.x - 1);

        float height = volumeBounds.max.y - volumeBounds.min.y;
        float yProbeSpacing = height / (float)(probeResolution.y - 1);

        float depth = volumeBounds.max.z - volumeBounds.min.z;
        float zProbeSpacing = depth / (float)(probeResolution.z - 1);

        probeSpacing = new Vector3(xProbeSpacing, yProbeSpacing, zProbeSpacing);

        float eps = .05f;

        probeVectorPositions = new List<Vector4>();
        List<Transform> probeTransforms = new List<Transform>();
        probeColors = new List<Color>();
        int index = 0;

        for (float y = volumeBounds.min.y; y < volumeBounds.max.y + eps; y += yProbeSpacing)
        {
            for (float z = volumeBounds.min.z; z < volumeBounds.max.z + eps; z += zProbeSpacing)
            {
                for (float x = volumeBounds.min.x; x < volumeBounds.max.x + eps; x += xProbeSpacing)
                {

                    Vector3 pos = new Vector3(x, y, z);
                    GameObject probeGO = Instantiate(probe, pos, Quaternion.identity, probeHolder);
                    probeVectorPositions.Add(new Vector4(pos.x, pos.y, pos.z, 1));
                    probeTransforms.Add(probeGO.transform);
                    probeColors.Add(Random.ColorHSV());
                    if (!showProbes)
                        probeGO.GetComponent<Renderer>().enabled = false;

                    // probeGO.GetComponent<Renderer>().enabled = false;
                    //GameObject text = Instantiate(textMesh, pos + Vector3.up * .3f, Quaternion.identity, canvas.transform);
                    //TMPro.TextMeshProUGUI tm = text.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    //tm.text = pos + "";
                    index++;
                }
            }
        }

        // Probes ordered by increasing index on the x, then z, then y axises 

        return probeTransforms.ToArray();
    }

    private void InitializeCamera()
    {
        GameObject tmp_camera_object = new GameObject("tmp_camera");
        tmp_camera = tmp_camera_object.AddComponent<Camera>();
        tmp_camera.clearFlags = CameraClearFlags.Skybox;
        tmp_camera.nearClipPlane = 0.01f;
        tmp_camera.farClipPlane = 300;
        tmp_camera.cullingMask = camLayerMask;
        tmp_camera.enabled = false;
        tmp_camera.fieldOfView = 90;
        tmp_camera.aspect = 1.0f;
    }

    private void InitializeCubeArrays()
    {
        RenderTextureDescriptor descCubeArray = new RenderTextureDescriptor();
        descCubeArray.autoGenerateMips = false;
        descCubeArray.useMipMap = true;
        descCubeArray.bindMS = false;
        descCubeArray.colorFormat = RenderTextureFormat.ARGB32;
        descCubeArray.depthBufferBits = 0;
        descCubeArray.dimension = UnityEngine.Rendering.TextureDimension.CubeArray;
        descCubeArray.enableRandomWrite = false;
        descCubeArray.height = RGBD_WH;
        descCubeArray.width = RGBD_WH;
        descCubeArray.msaaSamples = 1;
        descCubeArray.sRGB = true;
        descCubeArray.volumeDepth = probePositions.Length * 6;
        textureCubeArray = new RenderTexture(descCubeArray);

        RenderTextureDescriptor descDepthCubeArray = new RenderTextureDescriptor();
        descDepthCubeArray.autoGenerateMips = false;
        descDepthCubeArray.useMipMap = false;
        descDepthCubeArray.bindMS = false;
        descDepthCubeArray.colorFormat = RenderTextureFormat.Depth;
        descDepthCubeArray.depthBufferBits = DEPTH_BITS;
        descDepthCubeArray.dimension = UnityEngine.Rendering.TextureDimension.CubeArray;
        descDepthCubeArray.enableRandomWrite = false;
        descDepthCubeArray.height = RGBD_WH;
        descDepthCubeArray.width = RGBD_WH;
        descDepthCubeArray.msaaSamples = 1;
        descDepthCubeArray.sRGB = false;
        descDepthCubeArray.volumeDepth = probePositions.Length * 6;
        textureCubeArrayDepth = new RenderTexture(descDepthCubeArray);
    }    

    private void InitializePlanarRTs()
    {
        RenderTextureDescriptor descCube = new RenderTextureDescriptor();
        descCube.autoGenerateMips = true;
        descCube.useMipMap = true;
        descCube.bindMS = false;
        descCube.colorFormat = RenderTextureFormat.ARGB32;
        descCube.depthBufferBits = 0;
        descCube.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
        descCube.enableRandomWrite = false;
        descCube.height = RGBD_WH;
        descCube.width = RGBD_WH;
        descCube.msaaSamples = 1;
        descCube.sRGB = true;
        descCube.volumeDepth = 1;
        textureRGB = new RenderTexture(descCube);

        RenderTextureDescriptor descDepthCube = new RenderTextureDescriptor();
        descDepthCube.autoGenerateMips = false;
        descDepthCube.useMipMap = false;
        descDepthCube.bindMS = false;
        descDepthCube.colorFormat = RenderTextureFormat.Depth;
        descDepthCube.depthBufferBits = DEPTH_BITS;
        descDepthCube.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
        descDepthCube.enableRandomWrite = false;
        descDepthCube.height = RGBD_WH;
        descDepthCube.width = RGBD_WH;
        descDepthCube.msaaSamples = 1;
        descDepthCube.sRGB = false;
        descDepthCube.volumeDepth = 1;
        textureDepth = new RenderTexture(descDepthCube);
    }

    private void InitializeCubeRTs()
    {
        RenderTextureDescriptor descCube = new RenderTextureDescriptor();
        descCube.autoGenerateMips = true;
        descCube.useMipMap = true;
        descCube.bindMS = false;
        descCube.colorFormat = RenderTextureFormat.ARGB32;
        descCube.depthBufferBits = 0;
        descCube.dimension = UnityEngine.Rendering.TextureDimension.Cube;
        descCube.enableRandomWrite = false;
        descCube.height = RGBD_WH;
        descCube.width = RGBD_WH;
        descCube.msaaSamples = 1;
        descCube.sRGB = true;
        descCube.volumeDepth = 6;
        textureRGB = new RenderTexture(descCube);
    }

    private void InitializeRTs()
    {
        InitializeCubeArrays();

        if(useCameraDepthTarget)
        {
            InitializePlanarRTs();
        }
        else
        {
            InitializeCubeRTs();
        }
        
    }

    private Vector4 ZBufferParams()
    {
        float x = tmp_camera.farClipPlane / tmp_camera.nearClipPlane - 1;
        float y = 1;
        float z = x / tmp_camera.farClipPlane;
        float w = 1 / tmp_camera.farClipPlane;
        return new Vector4(x, y, z, w);
    }

    private void Start()
    {
        probePositions = InitializeProbes();

        InitializeCamera();
        InitializeRTs();

        if (useCameraDepthTarget)
        {
            tmp_camera.SetTargetBuffers(textureRGB.colorBuffer, textureDepth.depthBuffer);
        }

        foreach(Material mat in GIMats)
        {
            mat.SetTexture("_ProbeCubes", textureCubeArray);
            mat.SetTexture("_ProbeCubesDepth", textureCubeArrayDepth);
            mat.SetVector("_ZBuffParams", ZBufferParams());
            mat.SetVectorArray("_ProbePositions", probeVectorPositions.ToArray());

        }

        diffuseDepthShader = Shader.Find("SH/LitDiffuseDepth");

        Shader.SetGlobalInt("_NumProbes", probeVectorPositions.Count);
        Shader.SetGlobalFloat("_MaxDistance", maxDistance);
        Shader.SetGlobalVector("_VolumeBoundsMin", probeBounds.min);
        Shader.SetGlobalVector("_ProbeSpacing", probeSpacing);
        Shader.SetGlobalInt("_ProbeResolutionX", probeResolution.x);
        Shader.SetGlobalInt("_ProbeResolutionZ", probeResolution.z);
        Shader.SetGlobalInt("_ProbeResolutionY", probeResolution.y);
        Shader.SetGlobalFloat("_Bias", bias);
        Shader.SetGlobalFloat("_GIFactor", giFactor);
    }

    private void LateUpdate()
    {
        if (realTimeRendering)
            RenderProbes();
        else
        {
            if (count < 2)
                RenderProbes();

            count++;
        }
    }

    private void RenderProbes()
    {
        for (int i = 0; i < probePositions.Length; i++)
        {
            Vector3 position = probePositions[i].position;
            tmp_camera.transform.position = position;

            if (useCameraDepthTarget)
            {
                for (int j = 0; j < 6; j++)
                {
                    switch (j)
                    {
                        case 0: tmp_camera.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up); break;
                        case 1: tmp_camera.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up); break;
                        case 2: tmp_camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward); break;
                        case 3: tmp_camera.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.back); break;
                        case 4: tmp_camera.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up); break;
                        case 5: tmp_camera.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up); break;
                        default: break;
                    }

                    tmp_camera.Render();

                    int jj = j;
                    if (j == 2) jj = 3;
                    else if (j == 3) jj = 2;

                    Graphics.CopyTexture(textureRGB, 0, textureCubeArray, 6 * i + jj);
                    Graphics.CopyTexture(textureDepth, 0, textureCubeArrayDepth, 6 * i + jj);
                }
            }
            else
            {
                Shader.SetGlobalVector("_Position", position);
                tmp_camera.SetReplacementShader(diffuseDepthShader, "");
                tmp_camera.RenderToCubemap(textureRGB);
                tmp_camera.ResetReplacementShader();

                for (int j = 0; j < 6; j++)
                {
                    int jj = j;
                    if (j == 2) jj = 3;
                    else if (j == 3) jj = 2;
                    
                    Graphics.CopyTexture(textureRGB, j, textureCubeArray, 6 * i + jj);
                }
            }
        }
    }

    private void LocateProbes()
    {
        Vector3 locatorPos = probeLocator.position;
        Vector3 offsetPos = locatorPos - probeBounds.min + probeSpacing;
        Vector3 multiplier = new Vector3(1f / probeSpacing.x, 1f / probeSpacing.y, 1f / probeSpacing.z);
        Vector3 multOffset = new Vector3(offsetPos.x * multiplier.x, offsetPos.y * multiplier.y, offsetPos.z * multiplier.z);
        Vector3Int probeOffsetInt = new Vector3Int(Mathf.FloorToInt(multOffset.x), Mathf.FloorToInt(multOffset.y), Mathf.FloorToInt(multOffset.z));


        //Debug.Log(probeOffsetInt);
        Vector3Int[] indices = new Vector3Int[directions.Length];
        for (int i = 0; i < directions.Length; i++)
        {
            indices[i] = directions[i] + probeOffsetInt;
            indices[i] = new Vector3Int((int)Mathf.Clamp(indices[i].x, 0, probeResolution.x - 1), (int)Mathf.Clamp(indices[i].y, 0, probeResolution.y - 1), (int)Mathf.Clamp(indices[i].z, 0, probeResolution.z - 1));
        }

        int directionIndex = 0;
        Color color = new Color(0, 0, 0);
        foreach (Vector3Int index in indices)
        {
            int i = index.x + index.z * probeResolution.x + index.y * probeResolution.x * probeResolution.z;

            probePositions[i].GetComponent<Renderer>().sharedMaterial = selectedMaterial;


            directionIndex++;
        }

        probeLocator.GetComponent<Renderer>().sharedMaterial.color = color * 2;
    }
}
