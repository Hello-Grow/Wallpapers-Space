using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GlareLensCdlFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Header("Glow")]
        public float highThreshold = 10f;
        public int highKernelSize = 6;
        public float lowThreshold = 1f;
        public int lowKernelSize = 9;
        [Range(0f, 5f)] public float glowIntensity = 0.3f;

        [Header("Lens Distortion")]
        [Range(0f, 0.1f)] public float distortionAmount = 0.01f;
        [Range(0f, 0.1f)] public float dispersionAmount = 0.02f;

        [Header("ASC-CDL")]
        public Vector3 cdlOffset = Vector3.zero;
        public Vector3 cdlSlope = Vector3.one;
        public Vector3 cdlPower = Vector3.one;
    }

    public Settings settings = new Settings();

    GlowPass highGlowPass;
    GlowPass lowGlowPass;
    DistortPass distortionPass;
    CdlPass cdlPass;

    public override void Create()
    {
        highGlowPass = new GlowPass("HighGlow", settings.highThreshold, settings.highKernelSize, settings.renderPassEvent);
        lowGlowPass = new GlowPass("LowGlow", settings.lowThreshold, settings.lowKernelSize, settings.renderPassEvent + 1);
        distortionPass = new DistortPass(settings.renderPassEvent + 2);
        cdlPass = new CdlPass(settings.renderPassEvent + 3);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        highGlowPass.Setup();
        lowGlowPass.Setup();
        distortionPass.Setup(highGlowPass.TempTarget, lowGlowPass.TempTarget, settings);
        cdlPass.Setup(settings);

        renderer.EnqueuePass(highGlowPass);
        renderer.EnqueuePass(lowGlowPass);
        renderer.EnqueuePass(distortionPass);
        renderer.EnqueuePass(cdlPass);
    }

    // ────────────────────────────────────────────────────────────────
    class GlowPass : ScriptableRenderPass
    {
        public RenderTargetHandle TempTarget { get; private set; }

        readonly string profilerTag;
        readonly float threshold;
        readonly int kernelSize;
        readonly Material material;
        RenderTargetIdentifier source;

        const string shaderName = "Hidden/URP/GlareLensCdl";

        public GlowPass(string tag, float threshold, int kernelSize, RenderPassEvent evt)
        {
            profilerTag = tag;
            this.threshold = threshold;
            this.kernelSize = kernelSize;
            renderPassEvent = evt;
            TempTarget.Init(tag);
            material = CoreUtils.CreateEngineMaterial(shaderName);
        }

        public void Setup() { }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor desc)
            => cmd.GetTemporaryRT(TempTarget.id, desc, FilterMode.Bilinear);

        public override void Execute(ScriptableRenderContext ctx, ref RenderingData data)
        {
            var cmd = CommandBufferPool.Get(profilerTag);
            source = data.cameraData.renderer.cameraColorTarget;

            material.SetFloat("_Threshold", threshold);
            material.SetInt("_KernelSize", kernelSize);

            cmd.Blit(source, TempTarget.Identifier(), material, 0);
            ctx.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
            => cmd.ReleaseTemporaryRT(TempTarget.id);
    }

    // ────────────────────────────────────────────────────────────────
    class DistortPass : ScriptableRenderPass
    {
        const string shaderName = "Hidden/URP/GlareLensCdl";
        readonly string profilerTag = "DistortPass";
        readonly Material material;
        RenderTargetHandle tempTarget;
        RenderTargetIdentifier source;
        RenderTargetHandle glowH, glowL;
        float distortion, dispersion, glowIntensity;

        public DistortPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
            tempTarget.Init("DistortTemp");
            material = CoreUtils.CreateEngineMaterial(shaderName);
        }

        public void Setup(RenderTargetHandle high, RenderTargetHandle low, Settings s)
        {
            glowH = high;
            glowL = low;
            distortion = s.distortionAmount;
            dispersion = s.dispersionAmount;
            glowIntensity = s.glowIntensity;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor desc)
            => cmd.GetTemporaryRT(tempTarget.id, desc, FilterMode.Bilinear);

        public override void Execute(ScriptableRenderContext ctx, ref RenderingData data)
        {
            var cmd = CommandBufferPool.Get(profilerTag);
            source = data.cameraData.renderer.cameraColorTarget;

            material.SetFloat("_Distortion", distortion);
            material.SetFloat("_Dispersion", dispersion);
            material.SetFloat("_GlowIntensity", glowIntensity);

            cmd.SetGlobalTexture("_GlareHighTex", glowH.Identifier());
            cmd.SetGlobalTexture("_GlareLowTex", glowL.Identifier());

            cmd.Blit(source, tempTarget.Identifier(), material, 1);
            cmd.Blit(tempTarget.Identifier(), source);

            ctx.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
            => cmd.ReleaseTemporaryRT(tempTarget.id);
    }

    // ────────────────────────────────────────────────────────────────
    class CdlPass : ScriptableRenderPass
    {
        const string shaderName = "Hidden/URP/GlareLensCdl";
        readonly string profilerTag = "CdlPass";
        readonly Material material;
        RenderTargetHandle tempTarget;
        RenderTargetIdentifier source;
        Vector3 offset, slope, power;

        public CdlPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
            tempTarget.Init("CdlTemp");
            material = CoreUtils.CreateEngineMaterial(shaderName);
        }

        public void Setup(Settings s)
        {
            offset = s.cdlOffset;
            slope = s.cdlSlope;
            power = s.cdlPower;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor desc)
            => cmd.GetTemporaryRT(tempTarget.id, desc, FilterMode.Bilinear);

        public override void Execute(ScriptableRenderContext ctx, ref RenderingData data)
        {
            var cmd = CommandBufferPool.Get(profilerTag);
            source = data.cameraData.renderer.cameraColorTarget;

            material.SetVector("_Offset", new Vector4(offset.x, offset.y, offset.z, 0));
            material.SetVector("_Slope", new Vector4(slope.x, slope.y, slope.z, 0));
            material.SetVector("_Power", new Vector4(power.x, power.y, power.z, 0));

            cmd.Blit(source, tempTarget.Identifier(), material, 2);
            cmd.Blit(tempTarget.Identifier(), source);

            ctx.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
            => cmd.ReleaseTemporaryRT(tempTarget.id);
    }
}
