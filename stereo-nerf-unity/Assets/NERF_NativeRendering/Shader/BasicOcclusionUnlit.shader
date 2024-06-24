Shader "Unlit/BasicOcclusionUnlit"
{
    Properties{
        _MainTex("Texture", 2D) = "white" {}
    }

    SubShader{
        Tags { "RenderType" = "Opaque" }
        LOD 100

    Pass {
        CGPROGRAM

        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"


        struct appdata {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
           
        };

        struct v2f {

            float4 vertex : SV_POSITION;
            float4 screenPosReproj : TEXCOORD2;
            float depth : TEXCOORD3;
            float2 uv: TEXCOORD0;
        };

        sampler2D _MainTex;
        float _FoVNeRFCam;
        float _NeRFCamWidth;
        float _NeFRCamHeight;

        // TODO: add multi compile shader option
        sampler2D _RightNeRFDepth;
        sampler2D _LeftNeRFDepth;
        sampler2D _RightCameraDepthTexture;
        sampler2D _LeftCameraDepthTexture;



        v2f vert(appdata v) {

            v2f o;
            o.vertex = UnityObjectToClipPos(v.vertex);

            o.screenPosReproj = ComputeScreenPos(v.vertex);

            o.depth = Linear01Depth(o.screenPosReproj.z);

            o.uv = v.uv;
            return o;
        }


        fixed4 frag(v2f i) : SV_Target {

            float4 color = tex2D(_MainTex, i.uv);

            // Get the eye index (0 for left, 1 for right)
            float eyeIndex = unity_StereoEyeIndex;
            if (eyeIndex == 0) {
                float depth_n_l = tex2D(_LeftNeRFDepth, i.screenPosReproj.xy / i.screenPosReproj.w).r;
                float depth_n_u = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_LeftCameraDepthTexture, i.screenPosReproj.xy / i.screenPosReproj.w));

                // when depth is larger, the object is occluded
                if (depth_n_u > depth_n_l) {
                    color = fixed4(0, 0, depth_n_l, 1);

                    //discard;
                }
            }

            if (eyeIndex == 1) {
                float depth_n_r = tex2D(_RightNeRFDepth, i.screenPosReproj.xy / i.screenPosReproj.w).r;
                float depth_n_u = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_RightCameraDepthTexture, i.screenPosReproj.xy / i.screenPosReproj.w));

                //color = fixed4(depth_n_r , 0, 0, 1);

                if (depth_n_u > depth_n_r) {
                    color = fixed4(0 , 0, depth_n_r, 1);

                   // discard;
                }
            }
 


            return color;
        }
        ENDCG
    }
    }

}
