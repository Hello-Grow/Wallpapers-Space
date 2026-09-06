Shader "Hidden/URP/GlareLensCdl"
{
    Properties
    {
        _MainTex       ("Base Map",           2D)    = "white" {}
        _Threshold     ("Threshold",          Float) = 1.0
        _KernelSize    ("Kernel Size",        Float) = 6.0
        _GlowIntensity ("Glow Intensity",     Float) = 0.3
        _Distortion    ("Distortion",         Float) = 0.01
        _Dispersion    ("Dispersion",         Float) = 0.02
        _GlareHighTex  ("High Glow Tex",      2D)    = "white" {}
        _GlareLowTex   ("Low Glow Tex",       2D)    = "white" {}
        _Offset        ("CDL Offset",         Vector)= (0,0,0,0)
        _Slope         ("CDL Slope",          Vector)= (1,1,1,0)
        _Power         ("CDL Power",          Vector)= (1,1,1,0)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalRenderPipeline" }
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "GlowPass"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragGlow
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float _Threshold;
            float _KernelSize;

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f Vert(uint id : SV_VertexID)
            {
                v2f o;
                o.uv = float2((id << 1) & 2, id & 2);
                o.pos = float4(o.uv * 2.0 - 1.0, 0, 1);
                return o;
            }

            float4 FragGlow(v2f IN) : SV_Target
            {
                float2 uv = IN.uv;
                float4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                float lum = dot(c.rgb, float3(0.299, 0.587, 0.114));
                float4 bright = lum > _Threshold ? c : float4(0,0,0,0);

                float2 texel = 1.0 / _ScreenParams.xy;
                float4 sum = bright;
                int k = (int)_KernelSize;

                for (int i = 1; i <= k; ++i)
                {
                    sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + texel * float2(i, 0));
                    sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - texel * float2(i, 0));
                    sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + texel * float2(0, i));
                    sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - texel * float2(0, i));
                }

                return sum / (1.0 + 4.0 * k);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DistortPass"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDistort
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);          SAMPLER(sampler_MainTex);
            TEXTURE2D(_GlareHighTex);     SAMPLER(sampler_GlareHighTex);
            TEXTURE2D(_GlareLowTex);      SAMPLER(sampler_GlareLowTex);

            float _Distortion;
            float _Dispersion;
            float _GlowIntensity;

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f Vert(uint id : SV_VertexID)
            {
                v2f o;
                o.uv = float2((id << 1) & 2, id & 2);
                o.pos = float4(o.uv * 2.0 - 1.0, 0, 1);
                return o;
            }

            float4 FragDistort(v2f IN) : SV_Target
            {
                float2 uv0 = IN.uv;

                float2 uv = uv0 * 2.0 - 1.0;
                uv += normalize(uv) * _Distortion;
                uv = uv * 0.5 + 0.5;

                float4 src = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv0);
                float4 gH  = SAMPLE_TEXTURE2D(_GlareHighTex, sampler_GlareHighTex, uv);
                float4 gL  = SAMPLE_TEXTURE2D(_GlareLowTex, sampler_GlareLowTex, uv);

                float2 off = (uv - 0.5) * _Dispersion;

                float r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + off).r;
                float g = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).g;
                float b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - off).b;

                float4 disp = float4(r, g, b, 1);

                return disp + (gH + gL) * _GlowIntensity;
            }
            ENDHLSL
        }

        Pass
        {
            Name "CdlPass"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragCdl
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            float4 _Offset;
            float4 _Slope;
            float4 _Power;

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f Vert(uint id : SV_VertexID)
            {
                v2f o;
                o.uv = float2((id << 1) & 2, id & 2);
                o.pos = float4(o.uv * 2.0 - 1.0, 0, 1);
                return o;
            }

            float4 FragCdl(v2f IN) : SV_Target
            {
                float4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float3 rgb = pow((c.rgb * _Slope.xyz) + _Offset.xyz, _Power.xyz);
                return float4(rgb, c.a);
            }
            ENDHLSL
        }
    }
}
