Shader "Unlit/TextureEdit"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_Color ("Color", Color) = (1, 1, 1, 1)
		_EditType("EditType", Float) = 0
		_Threshold ("Threshold", Float) = 1
		_TextureScale ("TextureScale", Float) = 1
		_Offset ("Offset", Vector) = (0, 0, 0, 0)
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
			#pragma multi_compile_fog
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				UNITY_FOG_COORDS(1)
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 _MainTex_TexelSize;
			fixed4 _Color;
			float _EditType;
			float _Threshold;
			float _TextureScale;
			float4 _Offset;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				float2 uv = (float2(0.5, 0.5) * (1-_TextureScale) + _Offset.xy * 0.5) + i.uv * _TextureScale;
				fixed4 col = tex2D(_MainTex, uv);

				if (_EditType == 3) {
					if (abs(dot(col.rgb, _Color.rgb)) < _Threshold) {
						col.rgb = fixed3(0, 0, 0);
					}
				}

				return pow(col, 1/2.2);
			}
			ENDCG
		}
	}
}
