// A figure's silhouette lit along its EDGE, for the flash of every hit
// (StageHitFlash).
//
// The outline carries the pulse at full strength and the interior only a light
// tint (_Interior), so a hit reads as light catching the creature rather than a
// cut-out. The per-pixel arithmetic is BodyLight.FlashWeight (pinned by
// BodyLightTests, which also reads the property defaults below).
//
// Under Resources so the material is found by path: no Shader.Find.
Shader "PrincesPalace/UIRimFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Interior ("Interior share", Range(0,1)) = 0.08
        _RimWidth ("Rim width (fraction of canvas height)", Range(0,0.1)) = 0.012
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
        Blend SrcAlpha OneMinusSrcAlpha

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
                fixed alpha = opaque * (_Interior + (1.0 - _Interior) * edge) * i.color.a;
                return fixed4(i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
