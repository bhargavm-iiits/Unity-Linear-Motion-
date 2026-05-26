Shader "Custom/GhibliOpaque" {
    Properties {
        _Color ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.0
        _Metallic ("Metallic", Range(0,1)) = 0.0
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
    }
    SubShader {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _EmissionMap;
        fixed4 _Color;
        half _Glossiness;
        half _Metallic;
        fixed4 _EmissionColor;

        struct Input {
            float2 uv_MainTex;
            float4 color : COLOR; // Capture vertex colors!
        };

        void surf (Input IN, inout SurfaceOutputStandard o) {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            
            // Ghibli vertex color channels are wind & layer masks, not albedo tints. Ignoring it preserves raw texture colors!
            // c *= IN.color; 
            
            o.Albedo = c.rgb;
            o.Alpha = c.a;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            
            // Emissive Layer
            fixed4 emissive = tex2D(_EmissionMap, IN.uv_MainTex) * _EmissionColor;
            o.Emission = emissive.rgb;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
