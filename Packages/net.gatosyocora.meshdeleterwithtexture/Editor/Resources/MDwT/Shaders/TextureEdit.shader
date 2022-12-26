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

		_CurrentPos ("Mouse Current Pos", Vector) = (0, 0, 0, 0)
		_StartPos ("Drag Start Pos", Vector) = (0, 0, 0, 0)
		_EndPos ("Drag End Pos", Vector) = (0, 0, 0, 0)
		_LineWidth ("Line Width", Float) = 0.002
		_PenSize("Pen Size", Float) = 0.01

		_UVMap ("UVMap Texture", 2D) = "black"{}
		_UVMapLineColor ("UVMap Line Color", Color) = (0, 0, 0, 1)

		_SelectTex ("Select Area Texture", 2D) = "black"{}

		[Toggle]
		_ApplyGammaCorrection ("Apply Gamma Correction", Float) = 1
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
			float4 _MainTex_Size;
			fixed4 _Color;
			float _Threshold;
			float _TextureScale;
			float4 _Offset;

			float4 _StartPos;
			float4 _EndPos;
			float4 _CurrentPos;

			float _LineWidth;

			float _PenSize;

			sampler2D _UVMap;
			float4 _UVMapLineColor;

			float _ApplyGammaCorrection;

			sampler2D _SelectTex;
			sampler2D _SelectAreaPatternTex;
			float _SelectAreaPatternTex_Size;

			int _IsEraser;
			int _IsStraightMode;
			
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

				// ガンマ補正を適用
				if (_ApplyGammaCorrection)
					col = pow(col, 1/2.2);

				float2 startPos = (float2(0.5, 0.5) * (1-_TextureScale) + _Offset.xy * 0.5) + _StartPos.xy * _TextureScale;
				float2 endPos = (float2(0.5, 0.5) * (1-_TextureScale) + _Offset.xy * 0.5) + _EndPos.xy * _TextureScale;

				col.rgb = lerp(col.rgb, fixed3(1, 0.7, 0), tex2D(_SelectTex, uv).x == 1 && tex2Dlod(_SelectAreaPatternTex, float4(uv * _MainTex_Size.x / _SelectAreaPatternTex_Size, 0, 0)).x == 1);

				// UVMapを表示
				col.rgb = lerp(col.rgb, _UVMapLineColor, tex2D(_UVMap, uv).r);

				// ペンカーソルを表示
				float raito = _MainTex_Size.x / _MainTex_Size.y;
				fixed4 cursorColor = _IsEraser ? fixed4(0, 0, 0, 1) : _IsStraightMode ? fixed4(1, 0, 1, 1) : fixed4(1, 1, 0, 1);
				if (distance (uv * float2(1, raito), _CurrentPos.xy * float2(1, raito)) <= _PenSize)
					col = cursorColor;

				if (_IsEraser) {
					if (distance(uv * float2(1, raito), _CurrentPos.xy * float2(1, raito)) <= _PenSize * 0.8f)
						col = fixed4(1, 1, 1, 1);
				}

				return col;
			}
			ENDCG
		}
	}
}
