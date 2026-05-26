Shader "Custom/GhibliFoliage" {
    Properties {
        _Color ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
        _Glossiness ("Smoothness", Range(0,1)) = 0.0
        _Metallic ("Metallic", Range(0,1)) = 0.0
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
    }
    SubShader {
        Tags { "Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf Standard keepalpha addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _EmissionMap;
        fixed4 _Color;
        fixed _Cutoff;
        half _Glossiness;
        half _Metallic;
        fixed4 _EmissionColor;

        struct Input {
            float2 uv_MainTex;
            float4 color : COLOR; // Capture vertex colors from the mesh!
        };

        void surf (Input IN, inout SurfaceOutputStandard o) {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            
            // Ghibli vertex color channels are wind & layer masks, not albedo tints. Ignoring it preserves raw texture colors!
            // c *= IN.color; 
            
            o.Albedo = c.rgb;
            
            // Use the Red channel of the texture as alpha because Ghibli leaf textures store the cutout mask in the grayscale channels!
            o.Alpha = tex2D(_MainTex, IN.uv_MainTex).r * _Color.a;
            
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            
            // Emissive Painterly Ambient Layer
            fixed4 emissive = tex2D(_EmissionMap, IN.uv_MainTex) * _EmissionColor;
            o.Emission = emissive.rgb;
            
            // Clip alpha for cutout transparency
            clip(c.a - _Cutoff);
        }
        ENDCG
    }
    FallBack "Transparent/Cutout/Diffuse"
}
