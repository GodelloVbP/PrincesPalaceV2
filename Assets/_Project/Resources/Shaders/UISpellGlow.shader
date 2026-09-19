// A spell layer drawn bright enough for Bloom to see it.
//
// WHY A SHADER IS NEEDED AT ALL. The Volume profile's Bloom threshold is 1.05
// (PipelineBuilder.BuildVolumeProfile) and URP raises it through
// GammaToLinear, so a pixel has to land above roughly 1.11 linear before any
// bloom happens. Nothing drawn on a Canvas can: a sprite texture tops out at
// white, and the vertex colour a CanvasRenderer carries is a Color32, so
// setting Image.color above 1 clamps before the fragment shader ever sees it.
// The threshold is not reachable from content -- only from a shader that
// multiplies AFTER the sample.
//
// A KNEE, NOT A FLAT MULTIPLY. There is no Tonemapping override in the
// profile, so everything above 1 clips to flat white in the uber pass. A flat
// 2x would push every midtone past 1 and turn a painted eruption into a white
// blob. Instead the boost is ramped in over the top end of the sprite's own
// luminance: below the knee the drawing is untouched, above it the hot core
// climbs past the bloom threshold and nothing else moves.
//
// float4 RATHER THAN fixed4, which UIHitFlash beside this can use because it
// never exceeds 1. fixed is a [-2, 2] low-precision type on the platforms that
// still have one and is aliased to half elsewhere; the whole point here is to
// leave [0, 1] deliberately, so the precision is stated rather than inherited.
//
// Lives under Resources so it can be Resources.Load<Shader>'d at runtime,
// which is how SpellVfxPlayer reaches it -- Shader.Find would need it in
// Always Included Shaders to survive a build.
Shader "PrincesPalace/UISpellGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // How far above 1 the hottest pixels are pushed. 0 is the identity and
        // renders exactly what UI/Default would.
        _Boost ("Glow boost", Float) = 0

        // Where the ramp starts, in sprite luminance. 0.72 keeps the painted
        // body of an effect untouched and lifts only the molten core.
        _Knee ("Glow knee", Range(0, 1)) = 0.72
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
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _Boost;
            float _Knee;

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

            float4 frag(v2f i) : SV_Target
            {
                float4 texel = tex2D(_MainTex, i.texcoord) * i.color;

                // Rec. 709 luma, the same weighting URP's own bloom prefilter
                // uses, so "what this shader calls bright" and "what the bloom
                // pass calls bright" are the same judgement.
                float luma = dot(texel.rgb, float3(0.2126, 0.7152, 0.0722));
                float hot = smoothstep(_Knee, 1.0, luma);

                texel.rgb *= 1.0 + _Boost * hot;
                return texel;
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
