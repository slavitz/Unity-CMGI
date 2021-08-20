Shader "CMGI/Unlit"
{
	Properties
    {
		_MainTex ("Texture", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,1)
    }
	SubShader
	{
		Tags { "RenderType"="Opaque" "LightMode"="ForwardBase" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "AutoLight.cginc"

			#pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight

            TextureCubeArray<float4> _ProbeCubes;
			TextureCubeArray<half4> _ProbeCubesDepth;
			SamplerState _LinearClamp;

			uniform int _NumProbes;
			uniform float4 _ProbePositions[100];
			uniform float4 _VolumeBoundsMin;
			uniform float4 _ProbeSpacing;
			uniform int _ProbeResolutionX;
			uniform int _ProbeResolutionY;
			uniform int _ProbeResolutionZ;
			uniform float _MaxDistance;
			uniform float4 _ZBuffParams;
			uniform float _Bias;
			uniform float _GIFactor;

			sampler2D _MainTex;
            fixed4 _Color;

			static const int3 directions[8] = { int3(-1, -1, 0), int3(0, -1, 0), int3(-1, -1, -1), int3(0, -1, -1),
												int3(-1, 0, 0),  int3(0, 0, 0), int3(-1, 0, -1), int3(0, 0, -1) };

			float interpolate1D(float v1, float v2, float x){
				return v1 * (1 - x) + v2 * x;
			}

			float interpolate2D(float v1, float v2, float v3, float v4, float x, float y)
			{
				float s = interpolate1D(v1, v2, x);
				float t = interpolate1D(v3, v4, x);
				return interpolate1D(s, t, y);
			}

			float interpolate3D(float v1, float v2, float v3, float v4, float v5, float v6, float v7, float v8, float x, float y, float z)
			{
				float s = interpolate2D(v1, v2, v3, v4, x, y);
				float t = interpolate2D(v5, v6, v7, v8, x, y);
				return interpolate1D(s, t, z);
			}

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

			float square(float x)
			{
				return x * x;
			}

			float3 square(float3 f)
			{
				return float3(f.x * f.x, f.y * f.y, f.z * f.z);
			}

			float4 DDGI(float3 worldPos, float3 worldNormal, float3 viewDir)
			{
				float4 irradiance = float4(0, 0, 0, 1.0);

				float3 offsetPos = worldPos - _VolumeBoundsMin + _ProbeSpacing;
				float3 multiplier = 1.0 / _ProbeSpacing;
				float3 multOffset = float3(offsetPos.x * multiplier.x, offsetPos.y * multiplier.y, offsetPos.z * multiplier.z);
				int3 probeOffsetInt = int3(floor(multOffset.x), floor(multOffset.y), floor(multOffset.z));
				float sumWeight = 0;

				for (int dIndex = 0; dIndex < 8; dIndex++)
				{
					int3 index = probeOffsetInt + directions[dIndex];
					index = clamp(index, int3(0,0,0), int3(_ProbeResolutionX - 1, _ProbeResolutionY - 1, _ProbeResolutionZ - 1));

					int probeIndex = index.x + index.z * _ProbeResolutionX + index.y * _ProbeResolutionX * _ProbeResolutionZ;
					
					float3 probePosition = _ProbePositions[probeIndex].xyz;
					float3 direction = normalize(probePosition - worldPos);

					float weight = square(max(0.0001, (dot(direction, worldNormal) + 1.0) * 0.5)) + 0.2;
					float3 interp = abs(probePosition - worldPos) / _ProbeSpacing.xyz;

					weight *= interpolate3D(1.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, interp.x, interp.y, interp.z);

					float4 probeColor = _ProbeCubes.SampleLevel(_LinearClamp, float4(worldNormal, probeIndex), 4);

					float3 probeToPoint = worldPos + ( viewDir) * _Bias - probePosition;
					float distToProbe = length(probeToPoint);
					float3 dir = probeToPoint / distToProbe;

					float mean = DistFromDir(dir, probeIndex);
					
					if (distToProbe > mean)
					{
						weight *= 0;
					}
					
					sumWeight += weight;
					irradiance.rgb += sqrt(probeColor.rgb) * weight;
				}

				irradiance.rgb = square(irradiance.rgb / sumWeight);
				return irradiance;

			}

			struct appdata {
                float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 texcoord: TEXCOORD0;            
            };

			struct v2f
			{
                float3 worldNormal : TEXCOORD0;
                float4 pos : SV_POSITION;
				float3 worldPos : TEXCOORD1;
				float3 viewDir : TEXCOORD2;
                float2 uv : TEXCOORD4;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
				o.worldPos = mul (unity_ObjectToWorld, v.vertex);
				o.viewDir = UnityWorldSpaceViewDir(o.worldPos);
				o.uv = v.texcoord;
				
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
				fixed4 col = tex2D(_MainTex, i.uv) * _Color;

				float3 worldPos = i.worldPos;
				float3 worldNormal = normalize(i.worldNormal);
				float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos.xyz);
				float4 gi = DDGI(worldPos, worldNormal, viewDir);

				return float4(gi.rgb, 1);
            }
			
			ENDCG
		}
	}
}
