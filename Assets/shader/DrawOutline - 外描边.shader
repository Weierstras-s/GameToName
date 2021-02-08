Shader "Unlit/DrawOutline"
{
    Properties
    {
        _MainTex("MainTex",2D) = "white"{}
        _OutLineColor ("OutLineColor", Color) = (1,1,1,1)
        _OutLineWidth("OutLineWidth",Range(0,5)) =0.1 
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _OutLineColor;
            float _OutLineWidth;
            float2 _MainTex_TexelSize;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                if(col.a>0.5||!_OutLineWidth) return col;
                float2 leftUV = i.uv+float2(-1,0)*_OutLineWidth*_MainTex_TexelSize;
                float2 rightUV = i.uv+float2(1,0)*_OutLineWidth*_MainTex_TexelSize;
                float2 upUV = i.uv+float2(0,1)*_OutLineWidth*_MainTex_TexelSize;
                float2 downUV = i.uv+float2(0,-1)*_OutLineWidth*_MainTex_TexelSize;
                float w = max(max(max(tex2D(_MainTex,leftUV).a,tex2D(_MainTex,rightUV).a),tex2D(_MainTex,upUV).a),tex2D(_MainTex,downUV).a);
                //col = w>0?_OutLineColor:col;
                col = lerp(_OutLineColor,col,1-w);
                return col;
            }
            ENDCG
        }
    }
}
