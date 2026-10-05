Shader "MotorBound/PrototypeSkidRubber"
{
    Properties
    {
        _Color ("Rubber color", Color) = (0.022, 0.024, 0.025, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+1" }
        Cull Back
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct VertexInput
            {
                float4 vertex : POSITION;
            };

            struct VertexOutput
            {
                float4 position : SV_POSITION;
            };

            VertexOutput Vert(VertexInput input)
            {
                VertexOutput output;
                output.position = UnityObjectToClipPos(input.vertex);
                return output;
            }

            fixed4 Frag(VertexOutput input) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
    Fallback "Legacy Shaders/Diffuse"
}
