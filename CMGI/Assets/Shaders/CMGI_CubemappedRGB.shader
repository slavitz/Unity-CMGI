Shader "CMGI/CubemappedRGB"
{
	Properties
	{
		_Indexer("Indexer", Float) = 0.0
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
			
			#include "UnityCG.cginc"

            TextureCubeArray<float4> _ProbeCubes;
			TextureCubeArray<half4> _ProbeCubesDepth;
			SamplerState _LinearClamp;

			uniform int _NumProbes;
			uniform float4 _ProbePositions[100];

			uniform float4 _ZBuffParams;
			int _UseDepthTarget;

			float _Indexer;

			struct appdata {
                float4 vertex : POSITION;
				float3 normal : NORMAL;             
            };

			struct v2f
			{
                float3 worldNormal : TEXCOORD0;
                float4 pos : SV_POSITION;
				float3 worldPos : TEXCOORD1;
				float3 worldRefl: TEXCOORD2;
            };

			float LinearDepth(float z)
			{
				return 1.0 / (_ZBuffParams.z * z + _ZBuffParams.w);
			}

			float DistFromDir(float3 dir, int index)
			{
				float3 clipPos = 0;
				float3 absDir = abs(dir);

				if (absDir.x > absDir.y && absDir.x > absDir.z)
				{
					if (dir.x > 0)
					{
						clipPos = float3(-dir.z, dir.y, dir.x);
					}
					else
					{
						clipPos = float3(dir.z, dir.y, -dir.x);
					}
				}
				else if (absDir.z > absDir.y && absDir.z > absDir.x)
				{
					if (dir.z > 0)
					{
						clipPos = float3(dir.x, dir.y, dir.z);
					}
					else
					{
						clipPos = float3(-dir.x, dir.y, -dir.z);
					}
				}
				else if (absDir.y > absDir.x && absDir.y > absDir.z)
				{
					if (dir.y > 0)
					{
						clipPos = float3(dir.x, -dir.z, dir.y);
					}
					else
					{
						clipPos = float3(dir.x, dir.z, -dir.y);
					}
				}

				float viewPosZ = LinearDepth(_ProbeCubesDepth.Sample(_LinearClamp, float4(dir, index)));
				float2 clipPosXY = (clipPos * (1.0 / clipPos.z)).xy;

				float viewPosY = viewPosZ * clipPosXY.y;
				float viewPosX = viewPosZ * clipPosXY.x;
				return length(float3(viewPosX, viewPosY, viewPosZ));
				
			}

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
				o.worldPos = mul (unity_ObjectToWorld, v.vertex);
				float3 worldViewDir = normalize(UnityWorldSpaceViewDir(o.worldPos));
				o.worldRefl = reflect(-worldViewDir, o.worldNormal);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
				float3 worldPos = i.worldPos;
				float3 worldNormal = i.worldNormal;
		
				float4 colAtDir = _ProbeCubes.Sample(_LinearClamp, float4(worldNormal, _Indexer));
				return fixed4(colAtDir.rgb, 1);
			
            }
			ENDCG
		}
	}
}
