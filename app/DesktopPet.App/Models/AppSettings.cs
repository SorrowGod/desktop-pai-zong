namespace DesktopPet.App.Models;

public sealed class AppSettings
{
    public string PetName { get; set; } = "派大星";
    public double PetScale { get; set; } = 1.0;
    public int Affection { get; set; } = 50;
    public double? PetLeft { get; set; }
    public double? PetTop { get; set; }
    public string ApiBase { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1";
    public string Model { get; set; } = "qwen3.6-flash";
    public string ProtectedApiKey { get; set; } = string.Empty;
    public bool AutoStart { get; set; }

    public void Normalize()
    {
        PetName = string.IsNullOrWhiteSpace(PetName) ? "派大星" : PetName.Trim();
        PetScale = Math.Clamp(PetScale, 1.0, 3.0);
        Affection = Math.Clamp(Affection, 0, 100);
        ApiBase = string.IsNullOrWhiteSpace(ApiBase)
            ? "https://dashscope.aliyuncs.com/compatible-mode/v1"
            : ApiBase.Trim();
        Model = string.IsNullOrWhiteSpace(Model) ? "qwen3.6-flash" : Model.Trim();
    }
}
