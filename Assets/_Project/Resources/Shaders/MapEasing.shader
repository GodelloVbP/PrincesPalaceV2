// The run map's eased wood: the same painting as the found one, drawn over it
// only where the party has been (docs/PLAN_RUN_MAP.md section 1, P6).
//
// CONTENT-SPACE, not vertex-space. uGUI batches vertices into canvas space, and
// the map scrolls, so a vertex position would slide under the mask. The pixel's
// place on the leg is rebuilt from uv0 instead: the Image covers the whole leg
// (_RectSize wide and tall, pinned top-centre), so uv maps linearly onto it. The
// painting then tiles down the leg exactly as the found tiles do (every second
// tile mirrored top to bottom, MapLayout.BackgroundTileIsMirrored).
//
// The mask is _Discs (x, y, radius, 0) in MapLayout's content coordinates, with
// MapEasing.Falloff as its per-disc curve (MapEasingTests pin both). Stencil and
// the clip rect are kept, because the viewport is a RectMask2D.
//
// The eased LOOK is a stand-in: the painting desaturated and warmed. A painted
// eased variant replaces the derivation below with a second texture.
//
// Under Resources so MapController finds it by path: no Shader.Find.
Shader "PrincesPalace/MapEasing"
{
    Properties
    {
        [PerRendererData] _MainTex ("Painting", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _EaseSaturation ("Saturation kept", Range(0,1)) = 0.35
        _EaseWarmth ("Warm tint", Color) = (1.06,1.0,0.86,1)
        _TileHeight ("One painting's height in content units", Float) = 1080
        _RectSize ("Content width and height", Vector) = (1920, 1080, 0, 0)
        _DiscCount ("Discs in use", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "False"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

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
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _EaseSaturation;
            fixed4 _EaseWarmth;
            float _TileHeight;
            float4 _RectSize;
            float _DiscCount;
            float4 _Discs[40];

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            // MapEasing.Falloff: solid inside half the radius, smoothstep to
            // nothing at the radius.
            float Falloff(float distance, float radius)
            {
                float inner = radius * 0.5;
                return 1.0 - smoothstep(inner, radius, distance);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Content-local position: x from the centre, y negative down.
                float2 at = float2((i.texcoord.x - 0.5) * _RectSize.x, -(1.0 - i.texcoord.y) * _RectSize.y);

                float cover = 0.0;
                int count = (int)_DiscCount;
                for (int k = 0; k < 40; k++)
                {
                    if (k >= count) break;
                    float4 disc = _Discs[k];
                    cover = max(cover, Falloff(distance(at, disc.xy), disc.z));
                }

                // The painting at this place, tiled and mirrored like the found
                // wood: odd tiles run bottom to top.
                float down = -at.y;
                float span = _TileHeight * 2.0;
                float within = down - floor(down / span) * span;
                if (within > _TileHeight) within = span - within;
                float2 uv = float2(at.x / _RectSize.x + 0.5, 1.0 - within / _TileHeight);

                fixed4 painted = tex2D(_MainTex, uv) + _TextureSampleAdd;
                float lum = dot(painted.rgb, float3(0.299, 0.587, 0.114));
                float3 eased = lerp(float3(lum, lum, lum), painted.rgb, _EaseSaturation) * _EaseWarmth.rgb;

                fixed4 colour = fixed4(eased, painted.a) * i.color;
                colour.a *= cover;

                #ifdef UNITY_UI_CLIP_RECT
                colour.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                return colour;
            }
            ENDCG
        }
    }
}
