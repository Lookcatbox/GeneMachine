Shader "Custom/HeightNormalLit"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _NormalTex ("Normal Texture", 2D) = "bump" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _LightDir ("Light Direction", Vector) = (-0.45, -0.35, 0.82, 0)
        _AmbientStrength ("Ambient Strength", Range(0,1)) = 0.58
        _DiffuseStrength ("Diffuse Strength", Range(0,4)) = 2.2
        _NormalStrength ("Normal Strength", Range(0,2)) = 1
        _CurvatureStrength ("Curvature Strength", Range(0,4)) = 1.5
        _HeightContrast ("Height Contrast", Range(0,4)) = 0.9
        _HighTintStrength ("High Tint Strength", Range(0,1.5)) = 0.2
        _LowTintStrength ("Low Tint Strength", Range(0,1.5)) = 0.25
        _LightingEnabled ("Lighting Enabled", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _NormalTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _LightDir;
            float _AmbientStrength;
            float _DiffuseStrength;
            float _NormalStrength;
            float _CurvatureStrength;
            float _HeightContrast;
            float _HighTintStrength;
            float _LowTintStrength;
            float _LightingEnabled;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseColor = tex2D(_MainTex, i.uv) * _Color;
                float4 terrainData = tex2D(_NormalTex, i.uv);
                float2 normalXY = terrainData.xy * 2.0 - 1.0;
                normalXY *= _NormalStrength;
                float normalZ = sqrt(saturate(1.0 - dot(normalXY, normalXY)));
                float3 normal = normalize(float3(normalXY, normalZ));
                float signedHeight = terrainData.b * 2.0 - 1.0;
                float relief = terrainData.a * 2.0 - 1.0;

                float3 lightDir = normalize(_LightDir.xyz);
                float flatDiffuse = dot(float3(0.0, 0.0, 1.0), lightDir);
                float diffuseDelta = dot(normal, lightDir) - flatDiffuse;
                float detailLighting = 0.72
                    + diffuseDelta * _DiffuseStrength
                    + relief * (_CurvatureStrength * 0.42);
                detailLighting = clamp(detailLighting, _AmbientStrength, 1.18);

                float heightLighting = 1.0 + signedHeight * (_HeightContrast * 0.36);
                heightLighting = clamp(heightLighting, 0.72, 1.28);

                float lightingEnabled = saturate(_LightingEnabled);
                detailLighting = lerp(1.0, detailLighting, lightingEnabled);
                heightLighting = lerp(1.0, heightLighting, lightingEnabled);

                baseColor.rgb *= detailLighting;
                baseColor.rgb *= heightLighting;
                return baseColor;
            }
            ENDCG
        }
    }
}