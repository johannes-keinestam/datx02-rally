float4x4 World;
float4x4 View;
float4x4 Projection;

struct VertexShaderInput
{
    float4 Position : POSITION0;
	float2 TexCoord : TEXCOORD;
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
	float2 Depth : TEXCOORD0;
	float2 TexCoord : TEXCOORD1;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;

    float4 worldPosition = mul(input.Position, World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);

    output.Depth.xy = output.Position.zw;
	output.TexCoord = input.TexCoord;

    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float4 depth = float4(1, 1, 1, 1);
	// Depth is stored as distance from camera / far plane distance to get value between 0 and 1
	depth.r = 1 - (input.Depth.x / input.Depth.y);

	return depth;
}

technique Technique1
{
    pass Pass1
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
