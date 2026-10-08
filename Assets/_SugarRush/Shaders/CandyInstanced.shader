// Candy props drawn with GPU instancing (PropInstancer): every copy brings its own colour
// (_BaseColor is an instanced property), so all the trees or gummy bears of one shape go in a
// single draw call whatever their colours. Vertex colours tell the parts apart: alpha 1 = tinted
// part (crown, gummy body; rgb multiplies the tint, e.g. dark eyes), alpha 0 = trunk colour.
// Soft wrapped lighting from the main light + ambient, a glossy highlight on the tinted part,
// a little glow, fog, and a gentle wind sway that grows with height (meshes are 1 unit tall).
// _SR_Shade (global, set by the track) darkens everything a little inside forest tunnels.
Shader "SugarRush/CandyInstanced"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _TrunkColor ("Trunk", Color) = (1, 0.95, 0.88, 1)
        _Gloss ("Gloss", Range(0, 1)) = 0.6
        _Glow ("Glow", Range(0, 1)) = 0.12
        _Wind ("Wind sway (m at the top)", Float) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _TrunkColor;
                float _Gloss;
                float _Glow;
                float _Wind;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            float _SR_Shade;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 color : COLOR;
                float fog : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                float h = saturate(i.positionOS.y);
                float phase = dot(origin.xz, float2(0.13, 0.17));
                float sway = sin(_Time.y * 1.1 + phase) * 0.65 + sin(_Time.y * 1.9 + phase * 1.7) * 0.35;
                ws.xz += float2(sway, sway * 0.55) * _Wind * h * h;
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.color = i.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                float tinted = i.color.a;
                float3 albedo = lerp(_TrunkColor.rgb, tint.rgb * i.color.rgb, tinted);
                Light light = GetMainLight();
                float3 n = normalize(i.normalWS);
                float wrap = saturate(dot(n, light.direction)) * 0.7 + 0.3;
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                float spec = pow(saturate(dot(n, normalize(light.direction + v))), 40.0) * _Gloss * tinted;
                float3 ambient = SampleSH(n);
                float3 col = albedo * (light.color * wrap + ambient * 0.55) + spec * light.color + albedo * _Glow * tinted;
                col *= 1.0 - saturate(_SR_Shade);
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
