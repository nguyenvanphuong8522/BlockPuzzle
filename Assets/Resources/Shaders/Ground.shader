// Endless ground computed from world position (the quad just follows the camera).
// With a tile texture (_UseTexture = 1) it repeats the texture; otherwise it draws a checkerboard.
Shader "Survivor/Ground"
{
    Properties
    {
        _MainTex ("Tile", 2D) = "white" {}
        _UseTexture ("Use Texture", Float) = 0
        _TileSize ("Tile Size (world units)", Float) = 6
        _ColorA ("Color A", Color) = (0.12, 0.16, 0.12, 1)
        _ColorB ("Color B", Color) = (0.14, 0.19, 0.14, 1)
        _LineColor ("Line Color", Color) = (0.18, 0.24, 0.18, 1)
        _CellSize ("Cell Size", Float) = 2
        _Tint ("Tint (multiplies the tile)", Color) = (1, 1, 1, 1)
        _Saturation ("Saturation (0 = gray, 1 = original)", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Opaque" }
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _UseTexture;
            float _TileSize;
            float4 _ColorA;
            float4 _ColorB;
            float4 _LineColor;
            float _CellSize;
            float4 _Tint;
            float _Saturation;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 world : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.world = mul(unity_ObjectToWorld, vertex).xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.world / _CellSize;
                float checker = fmod(abs(floor(p.x) + floor(p.y)), 2.0);
                float4 col = lerp(_ColorA, _ColorB, checker);
                float2 f = frac(p);
                float edge = min(min(f.x, 1.0 - f.x), min(f.y, 1.0 - f.y));
                col = lerp(_LineColor, col, smoothstep(0.0, 0.03, edge));

                float4 tile = tex2D(_MainTex, i.world / _TileSize); // texture wrap mode is Repeat
                // Darker / less colorful ground so characters, enemies and gems stand out (PlayerTuning "Nền").
                float gray = dot(tile.rgb, float3(0.299, 0.587, 0.114));
                tile.rgb = lerp(gray.xxx, tile.rgb, _Saturation) * _Tint.rgb;
                return lerp(col, tile, _UseTexture);
            }
            ENDCG
        }
    }
}
