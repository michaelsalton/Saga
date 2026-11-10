using UnityEngine;

namespace Saga.Rendering
{
    // builds and animates the FFT ocean's frequency-space spectrum; kernels live in Shaders/Water/OceanSpectrum.compute
    [ExecuteAlways]
    [AddComponentMenu("Saga/Rendering/Ocean Spectrum")]
    public class OceanSpectrum : MonoBehaviour
    {
        // the FFT stage needs a power of two; the 8x8 thread groups need a multiple of 8
        public enum Size { _64 = 64, _128 = 128, _256 = 256, _512 = 512 }

        [SerializeField] ComputeShader spectrumShader;
        [SerializeField] ComputeShader fftShader;

        [Header("Grid")]
        [Tooltip("Waves per side of the spectrum. Also the texel count of every output texture.")]
        [SerializeField] Size resolution = Size._256;

        [Tooltip("Metres the generated ocean covers before it tiles. Longest representable wave is this long.")]
        [Min(1f)]
        [SerializeField] float patchSize = 100f;

        [Header("Wind")]
        [Tooltip("Metres per second. Sets the longest wave the spectrum keeps: windSpeed^2 / g.")]
        [Min(0.1f)]
        [SerializeField] float windSpeed = 10f;

        [Tooltip("Degrees around Y, 0 = +X.")]
        [Range(0f, 360f)]
        [SerializeField] float windDirection = 30f;

        [Header("Spectrum")]
        [Tooltip("Phillips constant A. Wave height scales with its square root. ~0.006 gives about 0.5 m RMS height at 10 m/s wind.")]
        [Min(0f)]
        [SerializeField] float spectrumScale = 0.006f;

        [Tooltip("Metres. Waves shorter than this are faded out so they don't alias into shimmer.")]
        [Min(0f)]
        [SerializeField] float smallWaveCutoff = 0.5f;

        static readonly int InitialSpectrumID = Shader.PropertyToID("_InitialSpectrum");
        static readonly int SpectrumAID = Shader.PropertyToID("_SpectrumA");
        static readonly int SpectrumBID = Shader.PropertyToID("_SpectrumB");
        static readonly int ResolutionID = Shader.PropertyToID("_Resolution");
        static readonly int PatchSizeID = Shader.PropertyToID("_PatchSize");
        static readonly int WindSpeedID = Shader.PropertyToID("_WindSpeed");
        static readonly int WindDirectionID = Shader.PropertyToID("_WindDirection");
        static readonly int SpectrumScaleID = Shader.PropertyToID("_SpectrumScale");
        static readonly int SmallWaveCutoffID = Shader.PropertyToID("_SmallWaveCutoff");
        static readonly int OceanTimeID = Shader.PropertyToID("_OceanTime");
        static readonly int FFTTargetID = Shader.PropertyToID("_FFTTarget");
        static readonly int LogResolutionID = Shader.PropertyToID("_LogResolution");
        static readonly int SpatialAID = Shader.PropertyToID("_SpatialA");
        static readonly int SpatialBID = Shader.PropertyToID("_SpatialB");
        static readonly int DisplacementID = Shader.PropertyToID("_Displacement");
        static readonly int SlopeID = Shader.PropertyToID("_Slope");
        static readonly int OceanDisplacementID = Shader.PropertyToID("_OceanDisplacement");
        static readonly int OceanSlopeID = Shader.PropertyToID("_OceanSlope");
        static readonly int OceanPatchSizeID = Shader.PropertyToID("_OceanPatchSize");

        RenderTexture initialSpectrum;
        RenderTexture spectrumA;
        RenderTexture spectrumB;
        RenderTexture displacement;
        RenderTexture slope;
        int initialKernel;
        int timeKernel;
        int rowsKernel;
        int columnsKernel;
        int assembleKernel;

        public RenderTexture SpectrumA => spectrumA;
        public RenderTexture SpectrumB => spectrumB;
        public RenderTexture Displacement => displacement;
        public RenderTexture Slope => slope;
        public int Resolution => (int)resolution;
        public float PatchSize => patchSize;

        void OnEnable() => Build();
        void OnDisable() => Release();

        // rebuild when an inspector value changes; deferred because textures can't be created inside OnValidate
        void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Build(); };
#endif
        }

        void Build()
        {
            Release();
            if (spectrumShader == null || fftShader == null) return;

            int size = (int)resolution;
            // full float: the FFT's log2(N) passes of adds turn half-precision rounding into visible grain
            initialSpectrum = CreateTarget(size, RenderTextureFormat.RGFloat, "OceanInitialSpectrum");
            spectrumA = CreateTarget(size, RenderTextureFormat.ARGBFloat, "OceanSpectrumA");
            spectrumB = CreateTarget(size, RenderTextureFormat.ARGBFloat, "OceanSpectrumB");
            displacement = CreateOutput(size, RenderTextureFormat.ARGBHalf, "OceanDisplacement");
            slope = CreateOutput(size, RenderTextureFormat.RGHalf, "OceanSlope");

            initialKernel = spectrumShader.FindKernel("InitialSpectrum");
            timeKernel = spectrumShader.FindKernel("TimeSpectrum");
            rowsKernel = fftShader.FindKernel("InverseFFTRows");
            columnsKernel = fftShader.FindKernel("InverseFFTColumns");
            assembleKernel = fftShader.FindKernel("Assemble");

            SetSharedParameters();
            spectrumShader.SetTexture(initialKernel, InitialSpectrumID, initialSpectrum);
            spectrumShader.Dispatch(initialKernel, size / 8, size / 8, 1);
        }

        static RenderTexture CreateTarget(int size, RenderTextureFormat format, string targetName)
        {
            var target = new RenderTexture(size, size, 0, format)
            {
                name = targetName,
                enableRandomWrite = true,
                // spectrum texels are discrete waves; blending neighbours would invent waves that don't exist
                filterMode = FilterMode.Point,
                // k wraps: the texel past the last one is the first one again
                wrapMode = TextureWrapMode.Repeat,
            };
            target.Create();
            return target;
        }

        static RenderTexture CreateOutput(int size, RenderTextureFormat format, string targetName)
        {
            var target = new RenderTexture(size, size, 0, format)
            {
                name = targetName,
                enableRandomWrite = true,
                // mips so distant water samples an average instead of aliasing into shimmer
                useMipMap = true,
                autoGenerateMips = false,
                filterMode = FilterMode.Trilinear,
                // the patch tiles, so sampling past the edge must wrap
                wrapMode = TextureWrapMode.Repeat,
            };
            target.Create();
            return target;
        }

        void SetSharedParameters()
        {
            float windRadians = windDirection * Mathf.Deg2Rad;
            spectrumShader.SetInt(ResolutionID, (int)resolution);
            spectrumShader.SetFloat(PatchSizeID, patchSize);
            spectrumShader.SetFloat(WindSpeedID, windSpeed);
            spectrumShader.SetVector(WindDirectionID, new Vector2(Mathf.Cos(windRadians), Mathf.Sin(windRadians)));
            spectrumShader.SetFloat(SpectrumScaleID, spectrumScale);
            spectrumShader.SetFloat(SmallWaveCutoffID, smallWaveCutoff);
        }

        void Release()
        {
            if (initialSpectrum != null) initialSpectrum.Release();
            if (spectrumA != null) spectrumA.Release();
            if (spectrumB != null) spectrumB.Release();
            if (displacement != null) displacement.Release();
            if (slope != null) slope.Release();
            initialSpectrum = null;
            spectrumA = null;
            spectrumB = null;
            displacement = null;
            slope = null;

            // black = zero displacement and zero slope, so a disabled simulation leaves flat still water instead of a dangling texture
            Shader.SetGlobalTexture(OceanDisplacementID, Texture2D.blackTexture);
            Shader.SetGlobalTexture(OceanSlopeID, Texture2D.blackTexture);
        }

        void Update()
        {
            if (spectrumShader == null || fftShader == null || initialSpectrum == null) return;

            int size = (int)resolution;
            // same clock the water shader and WaterSurface read, so floaters and waves stay in step
            spectrumShader.SetFloat(OceanTimeID, WaterSurface.ShaderTime);
            spectrumShader.SetInt(ResolutionID, size);
            spectrumShader.SetFloat(PatchSizeID, patchSize);
            spectrumShader.SetTexture(timeKernel, InitialSpectrumID, initialSpectrum);
            spectrumShader.SetTexture(timeKernel, SpectrumAID, spectrumA);
            spectrumShader.SetTexture(timeKernel, SpectrumBID, spectrumB);
            spectrumShader.Dispatch(timeKernel, size / 8, size / 8, 1);

            fftShader.SetInt(ResolutionID, size);
            // rounded because Mathf.Log(256, 2) can land on 7.9999
            fftShader.SetInt(LogResolutionID, Mathf.RoundToInt(Mathf.Log(size, 2f)));
            RunInverseFFT(spectrumA);
            RunInverseFFT(spectrumB);

            fftShader.SetTexture(assembleKernel, SpatialAID, spectrumA);
            fftShader.SetTexture(assembleKernel, SpatialBID, spectrumB);
            fftShader.SetTexture(assembleKernel, DisplacementID, displacement);
            fftShader.SetTexture(assembleKernel, SlopeID, slope);
            fftShader.Dispatch(assembleKernel, size / 8, size / 8, 1);
            displacement.GenerateMips();
            slope.GenerateMips();

            Shader.SetGlobalTexture(OceanDisplacementID, displacement);
            Shader.SetGlobalTexture(OceanSlopeID, slope);
            Shader.SetGlobalFloat(OceanPatchSizeID, patchSize);
        }

        // one group per row, then one per column; separate dispatches so columns see finished rows
        void RunInverseFFT(RenderTexture target)
        {
            int size = (int)resolution;
            fftShader.SetTexture(rowsKernel, FFTTargetID, target);
            fftShader.Dispatch(rowsKernel, size, 1, 1);
            fftShader.SetTexture(columnsKernel, FFTTargetID, target);
            fftShader.Dispatch(columnsKernel, size, 1, 1);
        }
    }
}
