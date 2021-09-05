using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public struct Sphere
{
    public Vector3 position;
    public float radius;
    public Vector3 albedo;
    public Vector3 specular;
}

public class RayTracingMaster : MonoBehaviour
{
    public ComputeShader RayTracingShader;
    public Texture SkyboxTexture;
    public Light DirectionalLight;

    public int Seed = 0;
    [Range(0,1)]
    public float Reflective = 0.5f;
    public Vector2 SphereRadius = new Vector2(3.0f, 8.0f);
    public uint SpheresMax = 100;
    public float SpherePlacementRadius = 100.0f;
    private ComputeBuffer _sphereBuffer;

    [Range(0, 2)]
    public float SkyboxIntensity = 1;
    [Range(0, 12)]
    public int Bounces = 8;

    private RenderTexture _target;
    private Camera _camera;
    private uint _currentSample = 0;
    private Material _addMaterial;

    public List<SphereCollider> sphereObjs = new List<SphereCollider>();

    private void OnEnable()
    {
        _currentSample = 0;
        SetupScene();

    }

    private void OnDisable()
    {
        if (_sphereBuffer != null)
        {
            _sphereBuffer.Release();
        }
    }

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        sphereObjs = FindObjectsOfType<SphereCollider>().ToList();
    }

    private void Update()
    {
        if (transform.hasChanged)
        {
            _currentSample = 0;
            transform.hasChanged = false;
        }
        if (DirectionalLight.transform.hasChanged)
        {
            _currentSample = 0;
            DirectionalLight.transform.hasChanged = false;
        }
        // if (sphereObjs.Any(p => p.transform.hasChanged))
        // {
        //     _currentSample = 0;
        //     SetupScene2();
        //     sphereObjs.ForEach(p => p.transform.hasChanged = false);
        // }
    }

    private void SetupScene()
    {
        if (Seed != 0)
            Random.InitState(Seed);
        List<Sphere> spheres = new List<Sphere>();

        for (int i = 0; i < SpheresMax; i++)
        {
            Sphere sphere = new Sphere();

            sphere.radius = SphereRadius.x + Random.value * (SphereRadius.y - SphereRadius.x);
            Vector2 randomPos = Random.insideUnitCircle * SpherePlacementRadius;
            sphere.position = new Vector3(randomPos.x, sphere.radius, randomPos.y);

            bool isTouching = false;
            foreach (Sphere other in spheres)
            {
                float minDist = sphere.radius + other.radius;
                if (Vector3.SqrMagnitude(sphere.position - other.position) < minDist * minDist)
                {
                    isTouching = true;
                    break;
                }
            }

            if (isTouching)
                continue;

            Color color = Random.ColorHSV();
            bool metal = Random.value < Reflective;
            sphere.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
            sphere.specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.zero * 0.04f;

            spheres.Add(sphere);
        }
        _sphereBuffer = new ComputeBuffer(spheres.Count, 40);
        _sphereBuffer.SetData(spheres);
    }
    
    private void SetupScene2()
    {
        List<Sphere> spheres = new List<Sphere>();

        foreach (var obj in sphereObjs)
        {
            Sphere sphere = new Sphere();
            sphere.position = obj.transform.position;
            sphere.radius = obj.radius;
            var renderer = obj.GetComponent<Renderer>();
            var metal = renderer.material.GetFloat("_Metallic") > 0;
            var albedo = new Vector3(renderer.material.color.r, renderer.material.color.g, renderer.material.color.b);
            sphere.albedo = metal ? Vector3.zero : albedo;
            sphere.specular = metal ? albedo : Vector3.zero * 0.04f;

            spheres.Add(sphere);
        }


        if (_sphereBuffer != null)
            _sphereBuffer.Release();
        _sphereBuffer = new ComputeBuffer(spheres.Count, 40);
        _sphereBuffer.SetData(spheres);
    }

    private void SetShaderParameters()
    {
        RayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        RayTracingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        RayTracingShader.SetVector("_PixelOffset", _currentSample == 0 ? new Vector2(0.5f, 0.5f) : new Vector2(Random.value, Random.value));
        RayTracingShader.SetFloat("_SkyboxIntensity", SkyboxIntensity);
        RayTracingShader.SetInt("_Bounces", Bounces);
        Vector3 l = DirectionalLight.transform.forward;
        RayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, DirectionalLight.intensity));
        RayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();
        Render(destination);
    }

    private void Render(RenderTexture destination)
    {
        InitRenderTexture();

        RayTracingShader.SetTexture(0, "Result", _target);
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        if (_addMaterial == null)
        {
            _addMaterial = new Material(Shader.Find("Hidden/AddShader"));
        }

        _addMaterial.SetFloat("_Sample", _currentSample);
        Graphics.Blit(_target, destination, _addMaterial);
        _currentSample++;
    }

    private void InitRenderTexture()
    {
        if (_target == null || _target.width != Screen.width || _target.height != Screen.height)
        {
            if (_target != null)
            {
                _target.Release();
            }

            _target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();
            _currentSample = 0;
        }
    }
}
