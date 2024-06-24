Shader "Unlit/RenderTextureOcclusionEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color: COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float4 projPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            float _FoVNeRFCam;
            float _FoVMainCam;

            float _VFoVNeRFCam;
            float _VFoVMainCam;

            sampler2D _LeftNeRFDepth;
            sampler2D _LeftCameraDepthTexture;

            sampler2D _RightNeRFDepth;
            sampler2D _RightCameraDepthTexture;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; //TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                o.projPos = ComputeScreenPos(v.vertex);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                float eyeIndex = unity_StereoEyeIndex;

                //float h_p = _FoVNeRFCam / _FoVMainCam;
                //float h_f = (1 - (h_p)) * 0.5;
                //if ((0.5- h_p/2 < i.uv.x) && (i.uv.x < 0.5 + h_p/2)  && (0.5 - h_p / 2 < i.uv.y) && (i.uv.y < 0.5 + h_p / 2) ) {
                //    if (eyeIndex == 0) {
                //        float2 uv_c;
                //        uv_c.x = lerp((0.5 - h_p /2), (0.5 + h_p / 2) , (i.uv.x - (0.5- h_p /2)));
                //        uv_c.y = lerp((0.5 - h_p / 2), (0.5 + h_p / 2), (i.uv.y - (0.5 - h_p / 2)));
                //        float depth_n = tex2D(_LeftNeRFDepth, uv_c).r;
                //        float depth_u = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_LeftCameraDepthTexture, uv_c));
                //       // col = fixed4(depth_n, 0, 0, 1);
                //        if (depth_u > depth_n) {
                //            discard;
                //        }
                //    }

                //}

                if (eyeIndex == 0) {
                    float depth_n = tex2D(_LeftNeRFDepth, i.uv).r;
                    float depth_u = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_LeftCameraDepthTexture, i.uv));
                    //col = fixed4(depth_n, 0, 0, 1);

                    if (depth_u > depth_n) {
                        discard;
                    }
                }

                if (eyeIndex == 1) {
                    float depth_n = tex2D(_RightNeRFDepth, i.uv).r;
                    float depth_u = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_RightCameraDepthTexture, i.uv));
                    //col = fixed4(depth_n,0, 0, 1);

                    if (depth_u > depth_n) {
                        discard;
                    }
                }
                return col;
            }
            ENDCG
        }
    }
}
