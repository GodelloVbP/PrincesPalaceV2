// The silhouette-edge test both actor-light shaders share (UIBurnGlow,
// UIRimFlash): the mean alpha of two rings of eight neighbours, at _RimWidth
// and twice it, so a pixel just inside the outline reads less than 1.
//
// Include AFTER the shader declares _MainTex, _MainTex_TexelSize and
// _RimWidth. The caller passes its uv derivatives so the samples keep the mip
// the plain tex2D would pick even when the call sits behind a branch.
float NeighbourAlpha(float2 uv, float2 dx, float2 dy)
{
    float2 step = float2(_RimWidth * _MainTex_TexelSize.w * _MainTex_TexelSize.x, _RimWidth);
    float sum = 0.0;
    for (int ring = 1; ring <= 2; ring++)
    {
        for (int k = 0; k < 8; k++)
        {
            float a = k * 0.785398;
            float2 d = float2(cos(a), sin(a)) * step * ring;
            sum += tex2Dgrad(_MainTex, uv + d, dx, dy).a;
        }
    }
    return sum / 16.0;
}
