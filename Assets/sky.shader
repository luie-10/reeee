Shader "Skybox/Blend" {
    Properties {
        _Tint ("Tint Color", Color) = (.5, .5, .5, .5)
        [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
        _Rotation ("Rotation", Range(0, 360)) = 0
        _Blend ("Blend", Range(0, 1)) = 0.0
        [NoScaleOffset] _Tex1 ("Cubemap 1 (Current)", Cube) = "grey" {}
        [NoScaleOffset] _Tex2 ("Cubemap 2 (Next)", Cube) = "grey" {}
    }
    SubShader {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            samplerCUBE _Tex1;
            samplerCUBE _Tex2;
            half4 _Tint;
            half _Exposure;
            float _Rotation;
            float _Blend;

            float3 RotateAroundYInDegrees (float3 vertex, float degrees) {
                float alpha = degrees * UNITY_PI / 180.0;
                float sina, cosa;
                sincos(alpha, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                return float3(mul(m, vertex.xz), vertex.y).xzy;
            }

            struct appdata_t {
                float4 vertex : POSITION;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float3 texcoord : TEXCOORD0;
            };

            v2f vert (appdata_t v) {
                v2f o;
                float3 rotated = RotateAroundYInDegrees(v.vertex.xyz, _Rotation);
                o.vertex = UnityObjectToClipPos(rotated);
                o.texcoord = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                half4 tex1 = texCUBE(_Tex1, i.texcoord);
                half4 tex2 = texCUBE(_Tex2, i.texcoord);
                half4 c = lerp(tex1, tex2, _Blend);
                c.rgb = c.rgb * _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;
                return c;
            }
            ENDCG
        }
    }
}