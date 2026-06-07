using System.Drawing;
using System.IO;
using System.Text.Json;

namespace LMaxPrint;

public class ThemeSettings
{
    // UI 颜色配置
    public string WindowBg { get; set; } = " #404040";
    public string ButtonBg { get; set; } = " #5A5A5A";
    public string ButtonText { get; set; } = " #FFFFFF";
    public string CanvasBg { get; set; } = " #F0F0F0";
    public string PaperBg { get; set; } = " #FFFFFF";
    public string PaperBorder { get; set; } = " #D3D3D3";
    public string PrintAreaColor { get; set; } = " #1E90FF";
    public float PrintAreaThickness { get; set; } = 1.5f;

    // 【新增】窗口状态配置
    public int WindowWidth { get; set; } = 1024;
    public int WindowHeight { get; set; } = 768;

    public Color GetWindowBgColor() => ColorTranslator.FromHtml(WindowBg);
    public Color GetButtonBgColor() => ColorTranslator.FromHtml(ButtonBg);
    public Color GetButtonTextColor() => ColorTranslator.FromHtml(ButtonText);
    public Color GetCanvasBgColor() => ColorTranslator.FromHtml(CanvasBg);
    public Color GetPaperBgColor() => ColorTranslator.FromHtml(PaperBg);
    public Color GetPaperBorderColor() => ColorTranslator.FromHtml(PaperBorder);
    public Color GetPrintAreaColor() => ColorTranslator.FromHtml(PrintAreaColor);

    private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme.json");

    public static ThemeSettings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<ThemeSettings>(json) ?? new ThemeSettings();
            }
        }
        catch { }
        return new ThemeSettings();
    }

    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }
}