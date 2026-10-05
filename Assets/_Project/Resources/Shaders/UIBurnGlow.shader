// A burning figure's silhouette smouldering like a bed of coals (StageBurnGlow).
//
// The sprite's silhouette (its alpha, the vertex colour's RGB) drawn
// ADDITIVELY, but not flat: the light gathers on the silhouette's EDGE and at the
// FEET, with only a faint tint over the interior, so the creature keeps its own
// colours and the heat reads as coming off the outline and up from the ground.
// A slow shimmer runs along the edge so it is never a steady line.
//
// The per-pixel arithmetic is BodyLight.BurnWeight (pinned by BodyLightTests,
// which also reads the property defaults below). Actor textures are one canvas
// per actor with the ground at the bottom, so texcoord.y is "how high on the
// body" for every figure.
//
// Under Resources so the material is found by path: no Shader.Find.
Shader "PrincesPalace/UIBurnGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Interior ("Interior share", Range(0,1)) = 0.16
        _RimWidth ("Rim width (fraction of canvas height)", Range(0,0.1)) = 0.02
        _FeetReach ("Feet reach (texcoord y)", Range(0.05,1)) = 0.4
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _MainTex_TexelSize;
            float _Interior;
            float _RimWidth;
            float _FeetReach;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            // Mean alpha of two rings of eight neighbours, at _RimWidth and twice it.
            float NeighbourAlpha(float2 uv)
            {
                float2 step = float2(_RimWidth * _MainTex_TexelSize.w * _MainTex_TexelSize.x, _RimWidth);
                float sum = 0.0;
                for (int ring = 1; ring <= 2; ring++)
                {
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * 0.785398;
                        float2 d = float2(cos(a), sin(a)) * step * ring;
                        sum += tex2D(_MainTex, uv + d).a;
                    }
                }
                return sum / 16.0;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float opaque = tex2D(_MainTex, i.texcoord).a;
                float edge = saturate((1.0 - NeighbourAlpha(i.texcoord)) * 2.0);
                float feet = 1.0 - smoothstep(0.0, _FeetReach, i.texcoord.y);
                // Embers wandering along the outline.
                float shimmer = 0.8 + 0.2 * sin(_Time.y * 5.0 + i.texcoord.x * 31.0 + i.texcoord.y * 17.0);
                float interior = _Interior * (0.35 + 0.65 * feet);
                float rim = edge * (0.5 + 0.5 * feet) * shimmer;
                fixed alpha = opaque * saturate(interior + rim) * i.color.a;
                return fixed4(i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
