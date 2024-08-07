texture2D _AmbientOcclusionTexture;
sampler sampler_AmbientOcclusionTexture;

texture2D AOTexture()
{
    return _AmbientOcclusionTexture;
}

void GetImage_float(out texture2D img)
{
    img = AOTexture();
}

void GetImage_half(out texture2D img)
{
    img = AOTexture();
}
