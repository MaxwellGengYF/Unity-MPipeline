Shader "Hidden/GroundTruthAmbientOcclusion"
{
	CGINCLUDE
		#include "GTAO_Pass.cginc"
	ENDCG

	SubShader
	{
		ZTest Greater
		Cull Off
		ZWrite Off

		Pass 
		{ 
			Name"ResolveGTAO"
			CGPROGRAM 
				#pragma vertex vert
				#pragma fragment ResolveGTAO_frag
			ENDCG 
		}

		Pass 
		{ 
			Name"SpatialGTAO_X"
			CGPROGRAM 
				#pragma vertex vert
				#pragma fragment SpatialGTAO_X_frag
			ENDCG 
		}

		Pass 
		{ 
			Name"SpatialGTAO_Y"
			CGPROGRAM 
				#pragma vertex vert
				#pragma fragment SpatialGTAO_Y_frag
			ENDCG 
		}

		Pass 
		{ 
			Name"TemporalGTAO"
			CGPROGRAM 
				#pragma vertex vert
				#pragma fragment TemporalGTAO_frag
			ENDCG 
		}

		pass
		{
			Name "Upsample"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment UpSample_frag
			ENDCG
		}
	}
}

