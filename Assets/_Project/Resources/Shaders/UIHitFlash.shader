// Flat-colour silhouette of a sprite, for the on-hit flash.
//
// A shader is needed here rather than just tinting the Image white, because
// Image.color MULTIPLIES the texture — white tint is the identity, so it
// changes nothing at all. This keeps the sprite's alpha (so the flash is the
// figure's exact shape) and throws away its RGB, replacing it with the
// vertex colour. Driving the flash is then just animating that colour's
// alpha on an overlay Image.
//
// Lives under Resources so it can be Resources.Load<Shader>'d at runtime.
// Shader.Find would need it in Always Included Shaders to survive a build,
// which is a project-settings dependency this avoids.
Shader "PrincesPalace/UIHitFlash"
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
                // Alpha from the sprite, colour from the vertex/tint. This is
                // the whole trick: the shape is the character, the colour is
                // whatever the flash wants.
                fixed alpha = tex2D(_MainTex, i.texcoord).a * i.color.a;
                return fixed4(i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
