Shader "ShouShouPBR"
{
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_ClearCoat("Clearcoat", Range(0, 1)) = 0.5
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_ClearCoatSmoothness("Secondary Smoothness", Range(0, 1)) = 0.5
		_Occlusion("Occlusion Scale", Range(0,1)) = 1
		_Cutoff("Cut off", Range(0, 1)) = 0
		_NormalIntensity("Normal Intensity", Vector) = (1,1,0,0)
		_SpecularIntensity("Specular Intensity", Range(0,1)) = 0.04
		_MetallicIntensity("Metallic Intensity", Range(0, 1)) = 0.1
		_MinDist("Min Tessellation Dist", float) = 20
		_MaxDist("Max Tessellation Dist", float) = 50
		_Tessellation("Tessellation Intensity", Range(1, 63)) = 1
		_HeightmapIntensity("Heightmap Intensity", Range(0, 10)) = 0
		_TileOffset("Texture ScaleOffset", Vector) = (1,1,0,0)
		[NoScaleOffset]_MainTex ("Albedo (RGB)Mask(A)", 2D) = "white" {}
		[NoScaleOffset]_BumpMap("Normal Map", 2D) = "bump" {}
		[NoScaleOffset]_SpecularMap("R(Smooth)G(Spec)B(Occ)", 2D) = "white"{}
		[NoScaleOffset]_HeightMap("Height Map", 2D) = "black"{}
		_SecondaryTileOffset("Secondary ScaleOffset", Vector) = (1,1,0,0)
		[NoScaleOffset]_SecondaryMainTex("Secondary Albedo(RGB)Mask(A)", 2D) = "white"{}
		[NoScaleOffset]_SecondaryBumpMap("Secondary Normal", 2D) = "bump"{}
		[NoScaleOffset]_SecondarySpecularMap("Secondary Specuar", 2D) = "white"{}
		_EmissionMultiplier("Emission Multiplier", Range(0, 128)) = 1
		_EmissionColor("Emission Color", Color) = (0,0,0,1)
		[NoScaleOffset]_EmissionMap("Emission Map", 2D) = "white"{}
		[HideInInspector]_LightingModel("lm", Int) = 1
		[HideInInspector]_DecalLayer("dl", Int) = 0

		[HideInInspector]_UseTessellation("tess", Int) = 0
	}
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf StandardSpecular fullforwardshadows
		#pragma multi_compile __ USE_WHITE
        #pragma target 5.0
            struct Input
        {
            float2 uv_MainTex;
			float2 uv_DetailAlbedo;
			float3 worldPos;
        };
    	float _SpecularIntensity;
		float _MetallicIntensity;
    	float4 _EmissionColor;
		float _Occlusion;
		float _VertexScale;
		float _VertexOffset;
		float _Cutoff;
		float _EmissionMultiplier;
		sampler2D _BumpMap;
		sampler2D _SpecularMap;
		sampler2D _MainTex;
		sampler2D _DetailAlbedo; 
		sampler2D _DetailNormal;
		sampler2D _EmissionMap;
		float4 _TileOffset;
		float _Glossiness;
		float2 _NormalIntensity;
		float4 _Color;
		
        void surf (Input IN, inout SurfaceOutputStandardSpecular o) {
			float2 uv = IN.uv_MainTex;
			uv = uv * _TileOffset.xy + _TileOffset.zw;
			float2 detailUV = IN.uv_DetailAlbedo;
			#ifdef USE_WHITE
			o.Normal = float3(0,0,1);
			o.Albedo = 1;
			o.Smoothness = 0;
			o.Specular = 0;
			o.Emission = 0;
			float4 spec = tex2D(_SpecularMap,uv);
			o.Occlusion = lerp(1, spec.b, _Occlusion);
			#else
			float4 spec = tex2D(_SpecularMap,uv);
			float4 c = tex2D (_MainTex, uv);
			#if CUT_OFF
			clip(c.a * _Color.a - _Cutoff);
			#endif

			o.Normal = UnpackNormal(tex2D(_BumpMap,uv));
			o.Normal.xy *= _NormalIntensity.xy;
			o.Albedo = c.rgb;

			o.Albedo *= _Color.rgb;

			o.Alpha = 1;
			o.Occlusion = lerp(1, spec.b, _Occlusion);
			o.Specular = lerp(_SpecularIntensity, o.Albedo, _MetallicIntensity * spec.g); 
			o.Smoothness = _Glossiness * spec.r;
			o.Emission = _EmissionColor * tex2D(_EmissionMap, uv) * _EmissionMultiplier;
			#endif
		}
        ENDCG
    }
    FallBack "Diffuse"
	CustomEditor "ShouShouEditor"
}
