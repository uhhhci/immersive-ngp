Shader "Custom/BasicOcclusion"
{
    Properties
    {
        _NeRFLeftCameraPosition("Camera Position", Vector) = (0,0,0,0)
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows
        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_LeftNeRFDepth;
            float2 uv_LeftCameraDepthTexture;
            float3 worldPos;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        sampler2D _LeftNeRFDepth;
        sampler2D _LeftNeRFMainTex;
        sampler2D _LeftCameraDepthTexture;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {

            float depth_n = tex2D(_LeftNeRFDepth, IN.uv_LeftNeRFDepth).r;
            float depth_u = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_LeftCameraDepthTexture, IN.uv_LeftCameraDepthTexture));

            //float3 vertexPos = IN.worldPos;
            //float distToCamera = length(vertexPos - _WorldSpaceCameraPos);

            float distToCamera = -UnityWorldToViewPos(IN.worldPos).z;
            //float objectDepth = Linear01Depth(IN.worldPos.z);
            //o.Albedo = fixed3(depth_u, depth_u, depth_u);
            if (distToCamera < depth_n) {
                discard;
            }
            //else {
            //    // Albedo comes from a texture tinted by color
            //    fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            //    o.Albedo = c.rgb;
            //    // Metallic and smoothness come from slider variables
            //    o.Metallic = _Metallic;
            //    o.Smoothness = _Glossiness;
            //    o.Alpha = c.a;
            //}
            

        }
        ENDCG
    }
    FallBack "Diffuse"
}
