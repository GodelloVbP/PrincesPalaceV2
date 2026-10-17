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

            #include "UIRimNeighbour.cginc"

            fixed4 frag(v2f i) : SV_Target
            {
                float2 dx = ddx(i.texcoord);
                float2 dy = ddy(i.texcoord);
                float opaque = tex2Dgrad(_MainTex, i.texcoord, dx, dy).a;
                // A transparent pixel ends up with alpha 0 whatever the rim
                // says, so skip the sixteen neighbour samples for it.
                if (opaque <= 0.0) return fixed4(i.color.rgb, 0);
                float edge = saturate((1.0 - NeighbourAlpha(i.texcoord, dx, dy)) * 2.0);
                fixed alpha = opaque * (_Interior + (1.0 - _Interior) * edge) * i.color.a;
                return fixed4(i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
