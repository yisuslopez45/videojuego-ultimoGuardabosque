// Agua low-poly estilizada para URP.
// Barata: sin reflexiones ni refracción. Normales planas por derivadas (look facetado),
// olas por vértice, color por profundidad y espuma en orillas/rápidos.
// Vertex color: R = rápidos (espuma por pendiente), G = cauce (color más profundo), A = peso de las olas. UV0 = dirección del flujo (xz).
Shader "UltimoGuardabosque/AguaEstilizada"
{
    Properties
    {
        _ShallowColor ("Color poco profundo", Color) = (0.22, 0.58, 0.72, 0.78)
        _DeepColor ("Color profundo", Color) = (0.08, 0.26, 0.46, 0.94)
        _DepthMaxDistance ("Distancia de profundidad", Float) = 1.1
        _FoamColor ("Color espuma", Color) = (0.95, 1, 1, 1)
        _FoamDistance ("Espuma de orilla", Float) = 0.35
        _RapidsFoam ("Espuma en rápidos", Range(0, 1)) = 0.6
        _ChannelDepth ("Profundidad visual del cauce", Range(0, 1)) = 0.85
        _FlowSpeed ("Velocidad del flujo", Float) = 2.5
        _WaveAmplitude ("Amplitud de olas", Float) = 0.07
        _WaveFrequency ("Frecuencia de olas", Float) = 0.45
        _WaveSpeed ("Velocidad de olas", Float) = 1.0
        _SpecularColor ("Brillo especular", Color) = (1, 1, 1, 0.2)
        _Glossiness ("Dureza especular", Float) = 140
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                half4 _SpecularColor;
                float _DepthMaxDistance;
                float _FoamDistance;
                float _RapidsFoam;
                float _ChannelDepth;
                float _FlowSpeed;
                float _WaveAmplitude;
                float _WaveFrequency;
                float _WaveSpeed;
                float _Glossiness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 flow : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float2 flow : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                half4 color : COLOR;
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(input.positionOS.xyz);
                float t = _Time.y * _WaveSpeed;
                float f = _WaveFrequency;
                float wave = sin(ws.x * f + t) * 0.5
                           + sin(ws.z * f * 1.3 + t * 1.2) * 0.35
                           + sin((ws.x + ws.z) * f * 0.7 - t * 0.8) * 0.4;
                ws.y += wave * _WaveAmplitude * input.color.a;

                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.flow = input.flow;
                o.color = input.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Normal plana por cara (estilo low-poly).
                float3 n = normalize(cross(ddy(i.positionWS), ddx(i.positionWS)));
                n *= sign(n.y + 1e-5);

                // Profundidad del agua a partir del depth buffer.
                float2 suv = i.screenPos.xy / i.screenPos.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float depth = max(0.0, sceneEye - i.screenPos.w);
                float depth01 = saturate(depth / _DepthMaxDistance);

                // En los arroyos (vertex color G) se fuerza un tono más profundo para que se lea como cauce.
                half4 col = lerp(_ShallowColor, _DeepColor, max(depth01, i.color.g * _ChannelDepth));

                // Espuma de orilla con borde irregular animado.
                float2 p = i.positionWS.xz;
                float n1 = ValueNoise(p * 0.9 + _Time.y * 0.15);
                // En el cauce (G) la espuma de orilla es más fina para que la cascada no se vea cortada.
                float shore = 1.0 - saturate(depth / (_FoamDistance * (1.0 - i.color.g * 0.7)));
                float foam = step(0.55, shore + (n1 - 0.5) * 0.6);

                // Espuma de rápidos que se desplaza con la corriente.
                float2 flowUV = p * float2(0.7, 0.7) - i.flow * _Time.y * _FlowSpeed;
                float streaks = ValueNoise(flowUV) * 0.65 + ValueNoise(flowUV * 2.3) * 0.35;
                foam = max(foam, step(1.0 - i.color.r * _RapidsFoam, streaks) * step(0.05, i.color.r));

                // Iluminación simple: half-lambert + especular Blinn facetado.
                Light mainLight = GetMainLight();
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half ndl = saturate(dot(n, mainLight.direction)) * 0.5 + 0.5;
                float3 h = normalize(mainLight.direction + viewDir);
                half spec = pow(saturate(dot(n, h)), _Glossiness) * _SpecularColor.a;
                half3 ambient = SampleSH(n) * 0.35;

                half3 rgb = col.rgb * (mainLight.color * ndl + ambient) + spec * _SpecularColor.rgb * mainLight.color;
                half fresnel = pow(1.0 - saturate(dot(n, viewDir)), 4.0);
                half alpha = saturate(col.a + fresnel * 0.2);

                rgb = lerp(rgb, _FoamColor.rgb * (mainLight.color * 0.6 + 0.4), foam * _FoamColor.a);
                alpha = lerp(alpha, 1.0, foam * _FoamColor.a);

                rgb = MixFog(rgb, i.fogFactor);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
