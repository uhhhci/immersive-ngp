Shader "Unlit/ZEDRightDynaimicsTunneling"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
		// visibility parameter controls the mid-foveate region
		_MidFoveateRadius("MidFoveateRadius", Range(0.0,10.0)) = 10

		// CentralCoordinate controls the center of the foveate region
		_CentralCoord("CentralCoordinate", vector) = (0.5,0.5,0,0)

		// central radius conrtols the radius of the high resolution foveate region
		_CentralRadius("CentralRadius", Range(0.0,0.5)) = 0.4

        [Header(EFFECT SELECTION)][Space]
		[KeywordEnum(None, Kernel_Matrix, Color_Matrix, HSVC)] Effect("Effect Type", Float) = 0
		// Common Vars
		_FxBlend("Effect Blend", range(0,1)) = 1
		[HideInInspector]_PixDistMulAdd("Pixel Dist, Mul, Adder", vector) = (1,1,1,0)

		_Color("Effect Color", Color) = (1,1,1,1)
		[Header(BASIC SETTINGS)][Space]

		// Kernel Materix Effect Vars
		[HideInInspector]_0123("K0123", vector) = (0,0,0,0)
		[HideInInspector]_4567("K4567", vector) = (1,0,0,0)
		[HideInInspector]_89AB("K89AB", vector) = (0,0,0,0)
		[HideInInspector]_KMul("Kernel Multiplier", float) = 1

		// Color Matrix Effect Vars
		[HideInInspector]_C0("C0", vector) = (1,0,0,0)
		[HideInInspector]_C1("C1", vector) = (0,1,0,0)
		[HideInInspector]_C2("C2", vector) = (0,0,1,0)
		[HideInInspector]_C3("C3", vector) = (0,0,0,1)

		// Hue Saturation Value Contrast Vars
		[HideInInspector]_HSVC("HueSatValCont", vector) = (0,1,1,1)
    }
    SubShader
    {

        Tags { "Queue"="Background" 
               "RenderType"="Transparent"}
        LOD 100
        Cull Off
        Lighting Off
        ZWrite Off

        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
			#pragma target 2.0
			#pragma multi_compile Effect_None EFFECT_KERNEL_MATRIX EFFECT_COLOR_MATRIX EFFECT_HSVC
			#include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color    : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float4 vertex : SV_POSITION;
            };


			v2f vert(appdata i)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(i.vertex);
				o.uv = i.uv;
				o.color = i.color;
				return o;
			}

			fixed4 _Color;
			sampler2D _MainTex;
			float4 _MainTex_TexelSize;
			float _FxBlend;
			float4 _PixDistMulAdd;
            float _MidFoveateRadius;
			float4 _CentralCoord;
			float _CentralRadius;

#if EFFECT_KERNEL_MATRIX // Kernel 9 Convolution Matrix
			float _KMul;
			float4 _0123, _4567, _89AB; //To minimize vars
			float4 ApplyKernelMatrix(float2 uv, float2 pixelDist)
			{
				float2 l_uv = uv;
				float4 l_fx = tex2D(_MainTex, uv) * _4567.x;
				l_uv.x -= pixelDist.x;
				l_fx += tex2D(_MainTex, l_uv) * _4567.y;
				l_uv.y -= pixelDist.y;
				l_fx += tex2D(_MainTex, l_uv) * _0123.z;
				l_uv.x = uv.x;
				l_fx += tex2D(_MainTex, l_uv) * _0123.y;
				l_uv.x += pixelDist.x;
				l_fx += tex2D(_MainTex, l_uv) * _0123.x;
				l_uv.y = uv.y;
				l_fx += tex2D(_MainTex, l_uv) * _0123.w;
				l_uv.y += pixelDist.y;
				l_fx += tex2D(_MainTex, l_uv) * _4567.z;
				l_uv.x = uv.x;
				l_fx += tex2D(_MainTex, l_uv) * _4567.w;
				l_uv.x -= pixelDist.x;
				l_fx += tex2D(_MainTex, l_uv) * _89AB.x;
				return l_fx * _KMul;
			}
#endif

#if EFFECT_COLOR_MATRIX || EFFECT_HSVC
			float4x4 _ColorMat;
			float4 _C0, _C1, _C2, _C3;
			float4 ApplyColorMatrix(float4 color) 
			{
				_ColorMat[0] = _C0;
				_ColorMat[1] = _C1;
				_ColorMat[2] = _C2;
				_ColorMat[3] = _C3;
				float a = color.a;
				color = mul(color, _ColorMat);
				color.a = a;
				return color;
			}
#endif

			fixed4 frag(v2f i) : SV_Target
			{
				fixed4 c = tex2D(_MainTex, i.uv);
				float2 pixDist = _MainTex_TexelSize.xy * _PixDistMulAdd.xy;
				float4 fx = c;
#if EFFECT_KERNEL_MATRIX
				fx = ApplyKernelMatrix(i.uv, pixDist);
#endif
#if EFFECT_COLOR_MATRIX || EFFECT_HSVC
				fx = ApplyColorMatrix(fx);
#endif
				fx *= _PixDistMulAdd.z;
				fx += _PixDistMulAdd.w;
				fixed4 res = (fx * _Color * _FxBlend) + ((c * i.color) * (1 - _FxBlend));
				res.a = c.a;
                
				//serious algorithm
				// this fade from different side but one side 
				//res.a = clamp((1-i.uv.x)*_Visibility * i.uv.y*_Visibility * (1-i.uv.y)*_Visibility,0,1);

				// fun algorithm
				// this produces the star effects
				//res.a = clamp(abs(0.5 - i.uv.x) *(10- _Visibility) * abs(0.5-i.uv.y) * (10- _Visibility ) * _Visibility, 0, 1);

				// fun algorithm
				// this produces a curtain effect
				//res.a = clamp(abs(0.5 - i.uv.x) * ( _Visibility) * abs(0.5 + i.uv.y) * ( _Visibility) , 0, 1);

				// working with clamp function
				//res.a = clamp((i.uv.x) * _Visibility * (1 - i.uv.x) * _Visibility * i.uv.y * _Visibility * (1 - i.uv.y) * _Visibility, 0, 1);
				
				// now trying to lerp 
				// this works with the effect of expanding from the center
				// res.a = lerp(0, 1, clamp((i.uv.x) * _Visibility * (1 - i.uv.x) * _Visibility * i.uv.y * _Visibility * (1 - i.uv.y) * _Visibility, 0, 1));

				// now try applying lerp to the coordinates, so that the fade effect always occur on the edge. 
				//res.a = lerp(0, 1, (i.uv.x) * lerp(0,1, (i.uv.x)* _Visibility) * (1 - i.uv.x) * lerp(0, 1, (1 - i.uv.x) * _Visibility) * i.uv.y * lerp(0, 1, (i.uv.y) * _Visibility) * (1 - i.uv.y) * lerp(0, 1, (1 - i.uv.y) * _Visibility));

				// this effect takes into consideration of the central coordinate, with radius constraint

				if (distance(i.uv, _CentralCoord) < _CentralRadius) {

					res.a = 1;
				}
				else {

					res.a = lerp(1, 0, abs(distance(i.uv, _CentralCoord) - _CentralRadius)* _MidFoveateRadius);

				}

				return res;
			}
		ENDCG
		}
    }
}
