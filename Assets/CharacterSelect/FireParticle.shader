Shader "FightingGame/SelectScreenFire"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.color = v.color; o.uv = v.uv; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = (i.uv - .5) * 2;
                // Soft tapered flame shape, generated in the shader rather than using a sprite.
                p.x /= lerp(1.0, .4, i.uv.y);
                float softness = pow(saturate(1 - dot(p, p)), 2);
                return fixed4(i.color.rgb, i.color.a * softness);
            }
            ENDCG
        }
    }
}
