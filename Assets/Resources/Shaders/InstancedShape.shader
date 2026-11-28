// Draws circles, rings, rectangles and sprites (from an atlas) with GPU instancing.
// Per instance:
//   _Color  tint
//   _Shape  x = 0 circle / 1 rect / 2 ring / 3 sprite / 4 sector (pie slice facing +x),
//           y = ring / sector inner radius (0..1),  z = white flash (0..1),  w = sector half-angle (radians)
//   _UV     sprite rect inside _MainTex (x, y, width, height in 0..1)
Shader "Survivor/InstancedShape"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Shape)
                UNITY_DEFINE_INSTANCED_PROP(float4, _UV)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 texUv : TEXCOORD2;
                float4 color : COLOR;
                float4 shape : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2.0 - 1.0;
                float4 rect = UNITY_ACCESS_INSTANCED_PROP(Props, _UV);
                o.texUv = rect.xy + v.uv * rect.zw;
                o.color = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                o.shape = UNITY_ACCESS_INSTANCED_PROP(Props, _Shape);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float kind = i.shape.x;
                float isRect = step(0.5, kind) * step(kind, 1.5);
                float isRing = step(1.5, kind) * step(kind, 2.5);
                float isSprite = step(2.5, kind) * step(kind, 3.5);
                float isSector = step(3.5, kind);

                float d = length(i.uv);
                float aa = max(fwidth(d), 0.001);
                float circle = 1.0 - smoothstep(1.0 - aa, 1.0, d);
                float ring = circle * smoothstep(i.shape.y - aa, i.shape.y, d);
                // Sector: the ring part whose angle from +x is within the half-angle (soft edge).
                float ang = abs(atan2(i.uv.y, i.uv.x));
                float angAa = max(fwidth(ang), 0.002);
                float sector = circle * smoothstep(i.shape.y - aa, i.shape.y, d) * (1.0 - smoothstep(i.shape.w - angAa, i.shape.w, ang));
                float alpha = lerp(lerp(lerp(circle, ring, isRing), 1.0, isRect), sector, isSector);

                // Darken circle edges a little so overlapping enemies stay readable.
                float shade = lerp(lerp(1.0, 0.72, smoothstep(0.75, 1.0, d)), 1.0, max(max(isRect, isRing), isSector));
                float4 shapeColor = float4(i.color.rgb * shade, i.color.a * alpha);

                float4 spriteColor = tex2D(_MainTex, i.texUv) * i.color;

                float4 result = lerp(shapeColor, spriteColor, isSprite);
                result.rgb = lerp(result.rgb, float3(1, 1, 1), i.shape.z);
                return result;
            }
            ENDCG
        }
    }
}
