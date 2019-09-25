Shader "Maxwell/LightingBoard"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _MainTexIntensity("MainTex Intensity", float) = 1
        _SecondTex("Secondary Texture", 2D) = "white"{}
        _SecondTexIntensity("SecondaryTex Intensity", float) = 1
        _CutOffTex("CutOff Texture", 2D) = "white"{}
        _Cutoff("Cut Off", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue" = "AlphaTest"}
        LOD 100
        CGINCLUDE
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
        #pragma multi_compile __ FIRST_TEX_USE_UV1
        #pragma multi_compile __ SECOND_TEX_USE_UV1
        #pragma multi_compile __ BLEND_ADD
        #pragma multi_compile __ CUT_OFF
        #include "UnityCG.cginc"
        float4x4 _LastVp;
        float4x4 _NonJitterVP;
        StructuredBuffer<float3x4> _LastFrameModel;
        uint _OffsetIndex;
        cbuffer UnityPerMaterial
        {
            float4 _MainTex_ST;
            float4 _SecondTex_ST;
            float4 _CutOffTex_ST;
            float _Cutoff;
            float _MainTexIntensity;
            float _SecondTexIntensity;
        }
        sampler2D _MainTex;
        sampler2D _CutOffTex;
        sampler2D _SecondTex;
        ENDCG
        pass
        {
	    Stencil
	    {
		    Ref 0
		    WriteMask 15
		    Pass replace
		    comp always
	    }
        Name "GBuffer"
        Tags {"LightMode" = "GBuffer" "Name" = "GBuffer"}
        ZTest Equal
        ZWrite off
        Cull back
            CGPROGRAM
            #pragma vertex vert_surf
            #pragma fragment frag_surf
            struct v2f_surf {
                float4 pos : SV_POSITION;
                float4 texUV : TEXCOORD0; 
                #if CUT_OFF
                float2 cutoffUV : TEXCOORD1;
                #endif
            };
            struct appdata
            {
	            float4 pos : POSITION;
	            float2 uv0 : TEXCOORD0; 
  	            float2 uv1 : TEXCOORD1; 
            };
            v2f_surf vert_surf (appdata v) 
{
	v2f_surf o;
	o.pos = UnityObjectToClipPos(v.pos);
    #if FIRST_TEX_USE_UV1
    float2 mainTexUV = TRANSFORM_TEX(v.uv1, _MainTex);
    #else
    float2 mainTexUV = TRANSFORM_TEX(v.uv0, _MainTex);
    #endif
    #if SECOND_TEX_USE_UV1
    float2 secondTexUV = TRANSFORM_TEX(v.uv1, _SecondTex);
    #else
    float2 secondTexUV = TRANSFORM_TEX(v.uv0, _SecondTex);
    #endif
    #if CUT_OFF
    o.cutoffUV =  TRANSFORM_TEX(v.uv0, _CutOffTex);
    #endif
    o.texUV = float4(mainTexUV, secondTexUV);
	return o;
}
void frag_surf (v2f_surf i,
    out float4 outEmission : SV_Target3
) {
    #if CUT_OFF
	float clipValue = tex2D(_CutOffTex, i.cutoffUV).r;
	clip(clipValue - _Cutoff);
    #endif
    float3 mainTexCol = tex2D(_MainTex, i.texUV.xy).xyz;
    float3 secondTexCol = tex2D(_SecondTex, i.texUV.zw).xyz;
    
    #if BLEND_ADD
    outEmission = float4((mainTexCol * _MainTexIntensity + secondTexCol * _SecondTexIntensity) , 1);
    #else
	outEmission = float4(mainTexCol * _MainTexIntensity * secondTexCol * _SecondTexIntensity, 1);
    #endif
}
            ENDCG
        }
		Pass
		{
			Stencil
			{
				Ref 128
				WriteMask 128
				Comp always
				Pass replace
			}
			ZTest Equal
			Cull back
			ZWrite off
			Tags {"LightMode" = "MotionVector"}
			CGPROGRAM
			#pragma vertex vert_mv
			#pragma fragment frag_mv
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles

            struct appdata_mv
			{
				float4 vertex : POSITION;
                #if CUT_OFF
				float2 texcoord : TEXCOORD0;
                #endif
			};
			struct v2f_mv
			{
				float4 vertex : SV_POSITION;
                #if CUT_OFF
				float2 texcoord : TEXCOORD0;
                #endif
				float3 nonJitterScreenPos : TEXCOORD1;
				float3 lastScreenPos : TEXCOORD2;
			};

			v2f_mv vert_mv (appdata_mv v)
			{
				v2f_mv o;
				o.vertex = UnityObjectToClipPos(v.vertex);
			  float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
				o.nonJitterScreenPos = ComputeScreenPos(mul(_NonJitterVP, worldPos)).xyw;
				float4 lastWorldPos =  float4(mul(_LastFrameModel[_OffsetIndex], v.vertex), 1);
            o.lastScreenPos = ComputeScreenPos(mul(_LastVp, lastWorldPos)).xyw;
            #if CUT_OFF
			o.texcoord = TRANSFORM_TEX(v.texcoord, _CutOffTex);
            #endif
				return o;
			}

			
			float2 frag_mv (v2f_mv i)  : SV_TARGET
			{
                #if CUT_OFF
				float c = tex2D(_CutOffTex, i.texcoord).r;
				clip(c - _Cutoff);
                #endif
				float4 velocity = float4(i.nonJitterScreenPos.xy, i.lastScreenPos.xy) / float4(i.nonJitterScreenPos.zz, i.lastScreenPos.zz);
#if UNITY_UV_STARTS_AT_TOP
				return velocity.xw - velocity.zy;
#else
				return velocity.xy - velocity.zw;
#endif

			}
			ENDCG
		}
		Pass
		{
			ZTest less
			Cull back
			Tags {"LightMode" = "Depth"}
			CGPROGRAM
			#pragma vertex vert_depth
			#pragma fragment frag_depth
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles
            struct appdata_depthPrePass
			{
				float4 vertex : POSITION;
                #if CUT_OFF
				float2 texcoord : TEXCOORD0;
                #endif
			};
			struct v2f_depth
			{
				float4 vertex : SV_POSITION;
                #if CUT_OFF
				float2 texcoord : TEXCOORD0;
                #endif
			};

			v2f_depth vert_depth (appdata_depthPrePass v)
			{
				v2f_depth o;
				o.vertex = UnityObjectToClipPos(v.vertex);
                #if CUT_OFF
				o.texcoord = TRANSFORM_TEX(v.texcoord, _CutOffTex);
                #endif
				return o;
			}
			void frag_depth (v2f_depth i)
			{
                #if CUT_OFF
				float c = tex2D(_CutOffTex, i.texcoord).r;
				clip(c - _Cutoff);
                #endif
			}
			ENDCG
		}
    }
    CustomEditor "LightingBoardEditor"
}
