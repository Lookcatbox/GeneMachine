Shader "Custom/OverlayComposite"
{
    Properties
    {
        _TempLightTex ("Temp/Light Source", 2D) = "black" {}
        _ChemTex ("Chemical Source", 2D) = "black" {}
        _ShowTemp ("Show Temp", Range(0,1)) = 0
        _ShowLight ("Show Light", Range(0,1)) = 0
        _ShowChem ("Show Chemical", Range(0,1)) = 0

        _TempBlue ("Temp Blue", Color) = (0,0,1,1)
        _TempCyan ("Temp Cyan", Color) = (0,1,1,1)
        _TempGreen ("Temp Green", Color) = (0,1,0,1)
        _TempYellow ("Temp Yellow", Color) = (1,1,0,1)
        _TempOrange ("Temp Orange", Color) = (1,0.5,0,1)
        _TempRed ("Temp Red", Color) = (1,0,0,1)

        _TempBlueMax ("Temp Blue Max", Float) = -40
        _TempCyanMax ("Temp Cyan Max", Float) = -20
        _TempGreenMax ("Temp Green Max", Float) = 0
        _TempYellowMax ("Temp Yellow Max", Float) = 15
        _TempOrangeMin ("Temp Orange Min", Float) = 25
        _TempOrangeMax ("Temp Orange Max", Float) = 30
        _TempRedMin ("Temp Red Min", Float) = 35
        _TempEncodeMin ("Temp Encode Min", Float) = -40
        _TempEncodeMax ("Temp Encode Max", Float) = 35

        _LightDark ("Light Dark", Color) = (0,0,0,1)
        _LightBright ("Light Bright", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _TempLightTex;
            sampler2D _ChemTex;
            float4 _TempLightTex_ST;
            float _ShowTemp;
            float _ShowLight;
            float _ShowChem;

            float4 _TempBlue;
            float4 _TempCyan;
            float4 _TempGreen;
            float4 _TempYellow;
            float4 _TempOrange;
            float4 _TempRed;

            float _TempBlueMax;
            float _TempCyanMax;
            float _TempGreenMax;
            float _TempYellowMax;
            float _TempOrangeMin;
            float _TempOrangeMax;
            float _TempRedMin;
            float _TempEncodeMin;
            float _TempEncodeMax;

            float4 _LightDark;
            float4 _LightBright;

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
                o.uv = TRANSFORM_TEX(v.uv, _TempLightTex);
                return o;
            }

            float3 EvaluateTempGradient(float tempC)
            {
                if (tempC <= _TempBlueMax)
                    return _TempBlue.rgb;
                if (tempC <= _TempCyanMax)
                {
                    float denom = max(0.0001, _TempCyanMax - _TempBlueMax);
                    float t = saturate((tempC - _TempBlueMax) / denom);
                    return lerp(_TempBlue.rgb, _TempCyan.rgb, t);
                }
                if (tempC <= _TempGreenMax)
                {
                    float denom = max(0.0001, _TempGreenMax - _TempCyanMax);
                    float t = saturate((tempC - _TempCyanMax) / denom);
                    return lerp(_TempCyan.rgb, _TempGreen.rgb, t);
                }
                if (tempC <= _TempYellowMax)
                {
                    float denom = max(0.0001, _TempYellowMax - _TempGreenMax);
                    float t = saturate((tempC - _TempGreenMax) / denom);
                    return lerp(_TempGreen.rgb, _TempYellow.rgb, t);
                }
                if (tempC <= _TempOrangeMin)
                {
                    float denom = max(0.0001, _TempOrangeMin - _TempYellowMax);
                    float t = saturate((tempC - _TempYellowMax) / denom);
                    return lerp(_TempYellow.rgb, _TempOrange.rgb, t);
                }
                if (tempC <= _TempOrangeMax)
                    return _TempOrange.rgb;
                if (tempC <= _TempRedMin)
                {
                    float denom = max(0.0001, _TempRedMin - _TempOrangeMax);
                    float t = saturate((tempC - _TempOrangeMax) / denom);
                    return lerp(_TempOrange.rgb, _TempRed.rgb, t);
                }
                return _TempRed.rgb;
            }

            float4 SampleTempLightLayer(float2 uv)
            {
                fixed4 sample = tex2D(_TempLightTex, uv);
                float showTemp = step(0.5, _ShowTemp);
                float showLight = step(0.5, _ShowLight);
                float active = showTemp + showLight;
                if (active < 0.5)
                    return float4(0, 0, 0, 0);

                float total = max(0.0001, active);
                float3 rgb = 0;

                if (showTemp > 0.5)
                {
                    float denom = max(0.0001, _TempEncodeMax - _TempEncodeMin);
                    float tempC = _TempEncodeMin + sample.r * denom;
                    rgb += EvaluateTempGradient(tempC) * showTemp;
                }

                if (showLight > 0.5)
                {
                    rgb += lerp(_LightDark.rgb, _LightBright.rgb, sample.g) * showLight;
                }

                rgb /= total;
                float alpha = sample.a * saturate(active);
                return float4(rgb, alpha);
            }

            float4 AlphaOver(float4 top, float4 bottom)
            {
                float outA = top.a + bottom.a * (1.0 - top.a);
                if (outA <= 0.0001)
                    return float4(0, 0, 0, 0);
                float3 outRgb = (top.rgb * top.a + bottom.rgb * bottom.a * (1.0 - top.a)) / outA;
                return float4(outRgb, outA);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 result = float4(0, 0, 0, 0);
                float showTempLight = step(0.5, _ShowTemp) + step(0.5, _ShowLight);
                if (showTempLight > 0.5)
                    result = SampleTempLightLayer(i.uv);

                if (_ShowChem > 0.5)
                {
                    float4 chem = tex2D(_ChemTex, i.uv);
                    result = AlphaOver(chem, result);
                }

                return result;
            }
            ENDCG
        }
    }
}
