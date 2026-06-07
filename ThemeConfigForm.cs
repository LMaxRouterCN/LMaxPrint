using System.Drawing;
using System.Windows.Forms;

namespace LMaxPrint;

public class ThemeConfigForm : Form
{
    private ThemeSettings _settings;
    private TableLayoutPanel _layout = null!;

    public ThemeConfigForm(ThemeSettings settings)
    {
        _settings = settings;
        this.Text = "主题与颜色配置";
        this.Size = new Size(400, 450);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.FromArgb(50, 50, 50);
        this.ForeColor = Color.White;
        
        InitializeLayout();
    }

    private void InitializeLayout()
    {
        _layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8, Padding = new Padding(15) };
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        
        AddColorRow(0, "窗口背景", _settings.WindowBg, c => _settings.WindowBg = c);
        AddColorRow(1, "按钮背景", _settings.ButtonBg, c => _settings.ButtonBg = c);
        AddColorRow(2, "按钮文字", _settings.ButtonText, c => _settings.ButtonText = c);
        AddColorRow(3, "画布背景", _settings.CanvasBg, c => _settings.CanvasBg = c);
        AddColorRow(4, "纸张背景", _settings.PaperBg, c => _settings.PaperBg = c);
        AddColorRow(5, "纸张边框", _settings.PaperBorder, c => _settings.PaperBorder = c);
        AddColorRow(6, "虚线框颜色", _settings.PrintAreaColor, c => _settings.PrintAreaColor = c);
        
        _layout.Controls.Add(new Label { Text = "虚线框粗细", Anchor = AnchorStyles.Left, Margin = new Padding(5), ForeColor = Color.White }, 0, 7);
        var numThickness = new NumericUpDown { Minimum = 0.5M, Maximum = 10M, DecimalPlaces = 1, Increment = 0.5M, Value = (decimal)_settings.PrintAreaThickness, Margin = new Padding(5), Width = 100 };
        numThickness.ValueChanged += (s, e) => _settings.PrintAreaThickness = (float)numThickness.Value;
        _layout.Controls.Add(numThickness, 1, 7);

        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
        var btnCancel = new Button { Text = "取消", Width = 80, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(70,70,70), ForeColor = Color.White };
        var btnSave = new Button { Text = "保存并应用", Width = 100, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30,144,255), ForeColor = Color.White };
        btnSave.Click += (s, e) => this.DialogResult = DialogResult.OK;
        
        btnPanel.Controls.Add(btnCancel);
        btnPanel.Controls.Add(btnSave);
        
        this.Controls.Add(_layout);
        this.Controls.Add(btnPanel);
        this.AcceptButton = btnSave;
        this.CancelButton = btnCancel;
    }

    private void AddColorRow(int row, string label, string initialColor, Action<string> setter)
    {
        var lbl = new Label { Text = label, Anchor = AnchorStyles.Left, Margin = new Padding(5), ForeColor = Color.White };
        _layout.Controls.Add(lbl, 0, row);

        var btn = new Button { Text = "选择颜色...", Width = 120, Margin = new Padding(5), BackColor = ColorTranslator.FromHtml(initialColor), FlatStyle = FlatStyle.Flat };
        btn.FlatAppearance.BorderColor = Color.Gray;
        btn.ForeColor = GetContrastColor(btn.BackColor);

        btn.Click += (s, e) =>
        {
            using var dlg = new ColorDialog { Color = btn.BackColor, FullOpen = true };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                btn.BackColor = dlg.Color;
                btn.ForeColor = GetContrastColor(dlg.Color);
                setter(ColorTranslator.ToHtml(dlg.Color));
            }
        };
        _layout.Controls.Add(btn, 1, row);
    }

    private Color GetContrastColor(Color c)
    {
        return (c.R * 0.299M + c.G * 0.587M + c.B * 0.114M) > 128 ? Color.Black : Color.White;
    }
}