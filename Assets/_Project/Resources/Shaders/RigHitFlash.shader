// Flat-colour silhouette of a rig part, for the on-hit flash. The
// SpriteRenderer twin of UIHitFlash.shader -- same trick, same reason
// (SpriteRenderer.color MULTIPLIES the texture exactly like Image.color
// does, so a white tint changes nothing; see UIHitFlash.shader's own
// header). Kept as a SEPARATE shader rather than reused directly because
// this one drops the UI-specific `ZTest [unity_GUIZTestMode]` line --
// that built-in variable is only ever set by Canvas rendering, and using
// it on a plain SpriteRenderer draw would read whatever it last held from
// an unrelated UI pass.
//
// Swapped onto a part's SpriteRenderer for the flash's duration and back
// afterward (see RigHitFlash.cs) rather than driven by a MaterialPropertyBlock
// on the default sprite material: SpriteSkin deforms the MESH and is
// independent of the material, so the swap needs no per-part SpriteSkin
// duplicate.
//
// Lives under Resources so it can be Resources.Load<Material>'d at runtime.
Shader "PrincesPalace/RigHitFlash"
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
                // Alpha from the sprite, colour from the vertex/tint -- the
                // shape is the part, the colour is whatever the flash wants.
                fixed alpha = tex2D(_MainTex, i.texcoord).a * i.color.a;
                return fixed4(i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
