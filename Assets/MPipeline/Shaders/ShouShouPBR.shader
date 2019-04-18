Shader "ShouShouPBR"
{
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Occlusion("Occlusion Scale", Range(0,1)) = 1
		_Cutoff("Cut off", Range(0, 1)) = 0
		_SpecularIntensity("Specular Intensity", Range(0,1)) = 0.3
		_MetallicIntensity("Metallic Intensity", Range(0, 1)) = 0.1
		_MainTex ("Albedo (RGB)DetailMask(A)", 2D) = "white" {}
		_BumpMap("Normal Map", 2D) = "bump" {}
		_SpecularMap("R(Smoothness)G(Spec)B(AO)", 2D) = "white"{}
		_DetailAlbedo("Detail Albedo", 2D) = "white"{}
		_DetailNormal("Detail Normal", 2D) = "bump"{}
		_EmissionColor("Emission Color", Color) = (0,0,0,1)
		_EmissionMultiplier("Emission Multiplier", Range(0, 128)) = 1
		_EmissionMap("Emission Map", 2D) = "white"{}
	}
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf StandardSpecular fullforwardshadows
#pragma shader_feature DETAIL_ON
#pragma multi_compile _ CUT_OFF
        #pragma target 5.0
            struct Input
        {
            float2 uv_MainTex;
			float2 uv_DetailAlbedo;
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

		float _Glossiness;
		float4 _Color;


        void surf (Input IN, inout SurfaceOutputStandardSpecular o) {
			// Albedo comes from a texture tinted by color
			float2 uv = IN.uv_MainTex;// - parallax_mapping(IN.uv_MainTex,IN.viewDir);
			float2 detailUV = IN.uv_DetailAlbedo;
			float4 spec = tex2D(_SpecularMap,uv);
			float4 c = tex2D (_MainTex, uv);
			#if CUT_OFF
			clip(c.a * _Color.a - _Cutoff);
			#endif
#if DETAIL_ON
			float3 detailNormal = UnpackNormal(tex2D(_DetailNormal, detailUV));
			float4 detailColor = tex2D(_DetailAlbedo, detailUV);
#endif
			o.Normal = UnpackNormal(tex2D(_BumpMap,uv));
			o.Albedo = c.rgb;
#if DETAIL_ON
			o.Albedo = lerp(detailColor.rgb, o.Albedo, c.a) * _Color.rgb;
			o.Normal = lerp(detailNormal, o.Normal, c.a);
#else
			o.Albedo *= _Color.rgb;
#endif
			o.Alpha = 1;
			o.Occlusion = lerp(1, spec.b, _Occlusion);
			o.Specular = lerp(_SpecularIntensity * spec.g, o.Albedo * _SpecularIntensity * spec.g, _MetallicIntensity); 
			o.Smoothness = _Glossiness * spec.r;
			o.Emission = _EmissionColor * tex2D(_EmissionMap, uv) * _EmissionMultiplier;
		}
        ENDCG
    }
    FallBack "Diffuse"
	CustomEditor "ShouShouEditor"
}
