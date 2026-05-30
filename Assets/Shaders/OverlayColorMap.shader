Shader "Custom/OverlayColorMap"
{
    Properties
    {
        _MainTex ("Overlay Texture", 2D) = "white" {}
        _ShowTemp ("Show Temp", Range(0,1)) = 1
        _ShowLight ("Show Light", Range(0,1)) = 0

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
            float4 _MainTex_ST;
            float _ShowTemp;
            float _ShowLight;

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
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
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

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 sample = tex2D(_MainTex, i.uv);
                float showTemp = step(0.5, _ShowTemp);
                float showLight = step(0.5, _ShowLight);
                float total = max(0.0001, showTemp + showLight);

                float3 tempRgb = 0;
                if (showTemp > 0.5)
                {
                    float denom = max(0.0001, _TempEncodeMax - _TempEncodeMin);
                    float tempC = _TempEncodeMin + sample.r * denom;
                    tempRgb = EvaluateTempGradient(tempC);
                }

                float3 lightRgb = 0;
                if (showLight > 0.5)
                {
                    lightRgb = lerp(_LightDark.rgb, _LightBright.rgb, sample.g);
                }

                float3 rgb = (tempRgb * showTemp + lightRgb * showLight) / total;
                float alpha = sample.a * saturate(showTemp + showLight);
                return float4(rgb, alpha);
            }
            ENDCG
        }
    }
}
