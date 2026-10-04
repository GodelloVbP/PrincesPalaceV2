// A figure's silhouette lit from below like a bed of coals, for the burning
// body's glow (StageBurnGlow).
//
// UIHitFlash's silhouette (the sprite's alpha, the vertex colour's RGB), with
// two differences. It blends ADDITIVELY, because a flat orange laid over a
// brown treant at any alpha it can afford simply vanishes, while added light
// brightens the body and still reads on a dark one. And its strength follows
// the texture's own height: full at the feet, a fraction at the crown, so the
// heat rises off the ground instead of washing the whole figure the same.
// Actor textures are one canvas per actor with the ground at the bottom, so
// texcoord.y is "how high on the body" for every figure.
//
// Under Resources for the same reason as UIHitFlash: no Shader.Find.
Shader "PrincesPalace/UIBurnGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
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

            fixed4 frag(v2f i) : SV_Target
            {
                // The shape is the character; the heat is strongest at the
                // feet and falls to a quarter at the top of the canvas.
                float heat = lerp(1.0, 0.25, saturate(i.texcoord.y));
                fixed alpha = tex2D(_MainTex, i.texcoord).a * i.color.a * heat;
                return fixed4(i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
