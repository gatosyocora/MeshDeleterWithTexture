﻿Shader "Unlit/TextureEdit"
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
			float4 _MainTex_TexelSize;
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

			float4 _Points[1000];
			int _PointNum;
			float _IsSelectingArea;

			sampler2D _SelectTex;
			
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
				
				// 範囲選択用の枠を表示
				/*
				if ((abs(uv.x - _StartPos.x) <= _LineWidth || abs(uv.x - _EndPos.x) <= _LineWidth) && uv.y >= min(_StartPos.y, _EndPos.y)-_LineWidth && uv.y <= max(_StartPos.y, _EndPos.y)+_LineWidth ||
					(abs(uv.y - _StartPos.y) <= _LineWidth || abs(uv.y - _EndPos.y) <= _LineWidth) && uv.x >= min(_StartPos.x, _EndPos.x)-_LineWidth && uv.x <= max(_StartPos.x, _EndPos.x)+_LineWidth
				)
					col = fixed4(1, 0.7, 0, 1);

				int p1Index, p2Index;
				float pointRadius;
				for (int k = 0; k < _PointNum; k++) {
					
					if (k == 0)
						pointRadius = 0.01;
					else
						pointRadius = 0.005;

					if (_PointNum > 1) {
						if (k != _PointNum-1) {
							p1Index = k;
							p2Index = k+1;

							float4 p1 = _Points[p1Index];
							float4 p2 = _Points[p2Index];

							float innerP1 = dot(normalize(p2.xy-p1.xy), normalize(uv-p1.xy));
							float innerP2 = dot(normalize(p1.xy-p2.xy), normalize(uv-p2.xy));

							if (innerP1 > 0.99999 && innerP1 <= 1 && innerP2 > 0.9999 && innerP2)
								col = fixed4(1, 0.7, 0, 1);
						}
						// 閉路完成状態のときだけ始点と終点をつなぐ線を描画
						else if (!_IsSelectingArea)
						{
							p1Index = _PointNum-1;
							p2Index = 0;

							float4 p1 = _Points[p1Index];
							float4 p2 = _Points[p2Index];

							float innerP1 = dot(normalize(p2.xy-p1.xy), normalize(uv-p1.xy));
							float innerP2 = dot(normalize(p1.xy-p2.xy), normalize(uv-p2.xy));

							if (innerP1 > 0.99999 && innerP1 <= 1 && innerP2 > 0.9999 && innerP2)
								col = fixed4(1, 0.7, 0, 1);
						}

					}

					float2 p1 = _Points[k].xy;

					float raito = _MainTex_TexelSize.x / _MainTex_TexelSize.y;
					if (distance (uv * float2(1, raito), p1 * float2(1, raito)) <= pointRadius)
						col = fixed4(1, 0.7, 0, 1);

				}

				col.rgb = lerp(col.rgb, fixed3(1, 0.7, 0), tex2D(_SelectTex, uv));
				*/

				// UVMapを表示
				col.rgb = lerp(col.rgb, _UVMapLineColor, tex2D(_UVMap, uv).r);

				// ペンカーソルを表示
				float raito = _MainTex_TexelSize.x / _MainTex_TexelSize.y;
				if (distance (uv * float2(1, raito), _CurrentPos.xy * float2(1, raito)) <= _PenSize)
					col = fixed4(1, 1, 0, 1);

				return col;
			}
			ENDCG
		}
	}
}
