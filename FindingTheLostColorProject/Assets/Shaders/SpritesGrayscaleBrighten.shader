Shader "Custom/SpritesGrayscaleBrighten"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header("Grayscale Effect")]
        _EffectAmount ("Grayscale Amount", Range(0, 1)) = 1.0 // 0: 컬러, 1: 완전 흑백
        
        [Header("Brightness Boost")]
        _Brightness ("Brightness Boost", Range(0, 2)) = 0.0  // 0: 보통, 2: 완전 눈부시게 밝아짐 (Whiteout)
    }

    SubShader
    {
        Tags
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
            };
            
            fixed4 _Color;
            sampler2D _MainTex;
            float _EffectAmount;
            float _Brightness;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                
                // 1. 흑백(Grayscale) 계산 및 보간
                half gray = dot(c.rgb, half3(0.299, 0.587, 0.114));
                c.rgb = lerp(c.rgb, half3(gray, gray, gray), _EffectAmount);
                
                // 2. 점점 밝아지게 만드는 연산 (Brightness Boost)
                // 원래 이미지 알파 채널(c.a)을 기준으로 밝기를 추가하여 외곽선 영역 밖으로 빛이 새어나가지 않게 잠금
                c.rgb += _Brightness * c.a;
                
                // 3. 투명도 연동 처리
                c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }
}
