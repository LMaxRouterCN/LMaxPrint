using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Windows.Forms;
using System.IO;

namespace LMaxPrint;

public partial class Form1 : Form
{
    private FlowLayoutPanel toolBar = null!;
    private ImageCanvas canvas = null!;
    private StatusStrip statusStrip = null!;
    private ToolStripStatusLabel lblStatus = null!;

    private OpenFileDialog openFileDialog = null!;
    private SaveFileDialog saveFileDialog = null!;
    private PrintDocument printDocument = null!;
    private PageSetupDialog pageSetupDialog = null!;
    private PrintDialog printDialog = null!;

    private Bitmap? _renderedPrintBitmap;
    private ThemeSettings _theme = null!;

    public Form1()
    {
        InitializeComponent(); 
        _theme = ThemeSettings.Load();
        
        if (_theme.WindowWidth > 0 && _theme.WindowHeight > 0)
        {
            this.Width = _theme.WindowWidth;
            this.Height = _theme.WindowHeight;
        }
        
        InitializeCustomComponents();
        InitializePrintComponents();
        BindEvents();
        ApplyTheme();
    }

    private void InitializeCustomComponents()
    {
        statusStrip = new StatusStrip();
        lblStatus = new ToolStripStatusLabel("就绪 | 拖入图片或 Ctrl+O 导入");
        statusStrip.Items.Add(lblStatus);
        this.Controls.Add(statusStrip);

        canvas = new ImageCanvas { Dock = DockStyle.Fill };
        this.Controls.Add(canvas);

        toolBar = new FlowLayoutPanel { 
            Dock = DockStyle.Top, 
            AutoSize = true, 
            FlowDirection = FlowDirection.LeftToRight, 
            WrapContents = true, 
            Padding = new Padding(5) 
        };
        this.Controls.Add(toolBar);

        CreateButton("导入 (Ctrl+O)", OnImportClick);
        CreateButton("左转 (←)", (s, e) => canvas.Rotate(-90));
        CreateButton("右转 (→)", (s, e) => canvas.Rotate(90));
        CreateButton("水平镜像", (s, e) => canvas.ToggleMirrorX());
        CreateButton("垂直镜像", (s, e) => canvas.ToggleMirrorY());
        CreateButton("适应纸张 (R)", (s, e) => canvas.ResetTransform());
        CreateButton("页面设置", OnPageSetupClick);
        CreateButton("主题设置", OnThemeConfigClick);
        CreateButton("保存 (Ctrl+S)", OnSaveClick);
        CreateButton("打印 (Ctrl+P)", OnPrintClick);
    }

    private Button CreateButton(string text, EventHandler clickHandler)
    {
        var btn = new Button { Text = text, Width = 100, Height = 35, Margin = new Padding(5, 5, 0, 5), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        btn.FlatAppearance.BorderSize = 1;
        btn.Click += clickHandler;
        toolBar.Controls.Add(btn);
        return btn;
    }

    private void ApplyTheme()
    {
        this.BackColor = _theme.GetWindowBgColor();
        toolBar.BackColor = _theme.GetWindowBgColor();
        statusStrip.BackColor = _theme.GetWindowBgColor();
        lblStatus.ForeColor = _theme.GetButtonTextColor();
        
        foreach (Control c in toolBar.Controls)
        {
            if (c is Button btn)
            {
                btn.BackColor = _theme.GetButtonBgColor();
                btn.ForeColor = _theme.GetButtonTextColor();
                btn.FlatAppearance.BorderColor = ControlPaint.Dark(_theme.GetButtonBgColor());
                btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(_theme.GetButtonBgColor());
            }
        }
        
        canvas.ApplyTheme(_theme);
    }

    private void InitializePrintComponents()
    {
        openFileDialog = new OpenFileDialog { Filter = "图像文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.webp|所有文件|*.*", Title = "选择要导入的图片" };
        saveFileDialog = new SaveFileDialog { Filter = "PNG 图像|*.png|JPEG 图像|*.jpg|BMP 图像|*.bmp", Title = "保存渲染后的图像" };
        printDocument = new PrintDocument();
        printDocument.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        pageSetupDialog = new PageSetupDialog { Document = printDocument };
        printDialog = new PrintDialog { Document = printDocument, UseEXDialog = true };
        UpdateCanvasPageInfo();
    }

    private void UpdateCanvasPageInfo()
    {
        var ps = printDocument.DefaultPageSettings;
        float w = ps.Bounds.Width / 100f;
        float h = ps.Bounds.Height / 100f;
        
        if (ps.Landscape && w < h) { float temp = w; w = h; h = temp; }
        else if (!ps.Landscape && w > h) { float temp = w; w = h; h = temp; }
        
        canvas.SetPageInfo(w, h, 
            ps.Margins.Top / 100f, ps.Margins.Bottom / 100f, 
            ps.Margins.Left / 100f, ps.Margins.Right / 100f);
    }

    private void BindEvents()
    {
        canvas.ImageChanged += (s, e) => { var img = canvas.GetOriginalImage(); if (img != null) lblStatus.Text = $"已加载 | 原始: {img.Width}x{img.Height}"; };
        canvas.TransformChanged += (s, e) => lblStatus.Text = "视图已更新 | 虚线框内为最终打印区域";
        canvas.PhysicalSizeChanged += (s, sizeStr) => lblStatus.Text = $"🎯 {sizeStr} | 左键拖图，滚轮缩放图 | Ctrl+滚轮缩放画布，中键/右键拖动画布";
        printDocument.PrintPage += PrintDocument_PrintPage;
    }

    private void OnImportClick(object? sender, EventArgs e) { if (openFileDialog.ShowDialog() == DialogResult.OK) canvas.LoadImage(openFileDialog.FileName); }

    private void OnThemeConfigClick(object? sender, EventArgs e)
    {
        using var frm = new ThemeConfigForm(_theme);
        if (frm.ShowDialog(this) == DialogResult.OK)
        {
            _theme.Save();
            ApplyTheme();
        }
    }

    private void OnSaveClick(object? sender, EventArgs e)
    {
        var img = canvas.GetPrintableImage();
        if (img == null) { MessageBox.Show("请先导入图片。"); return; }
        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var path = saveFileDialog.FileName;
                var format = ImageFormat.Png;
                if (path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)) format = ImageFormat.Jpeg;
                else if (path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)) format = ImageFormat.Bmp;
                Task.Run(() => img.Save(path, format)).ContinueWith(t => this.Invoke(() => lblStatus.Text = t.IsFaulted ? "保存失败" : "保存成功"));
            }
            catch (Exception ex) { MessageBox.Show($"保存出错: {ex.Message}"); }
        }
    }

    private void OnPageSetupClick(object? sender, EventArgs e)
    {
        if (pageSetupDialog.ShowDialog() == DialogResult.OK) 
        { 
            var ps = pageSetupDialog.PageSettings;
            if (ps == null) return;
            printDocument.DefaultPageSettings = ps;
            
            var m = ps.Margins;
            if (m.Left < 0 || m.Right < 0 || m.Top < 0 || m.Bottom < 0 ||
                m.Left + m.Right > ps.Bounds.Width || m.Top + m.Bottom > ps.Bounds.Height) {
                ps.Margins = new Margins(0, 0, 0, 0);
            }

            float w = ps.Bounds.Width / 100f;
            float h = ps.Bounds.Height / 100f;
            if (ps.Landscape && w < h) { float temp = w; w = h; h = temp; }
            else if (!ps.Landscape && w > h) { float temp = w; w = h; h = temp; }
            
            canvas.SetPageInfo(w, h, 
                ps.Margins.Top / 100f, ps.Margins.Bottom / 100f, 
                ps.Margins.Left / 100f, ps.Margins.Right / 100f);
                
            lblStatus.Text = $"页面更新: {ps.PaperSize.PaperName}"; 
        }
    }

    private void OnPrintClick(object? sender, EventArgs e)
    {
        if (canvas.GetOriginalImage() == null) { MessageBox.Show("请先导入图片。"); return; }
        if (printDialog.ShowDialog() == DialogResult.OK)
        {
            try 
            { 
                lblStatus.Text = "正在生成物理裁剪图像...";
                this.Update(); 
                PreRenderForPrint();
                printDocument.Print(); 
                lblStatus.Text = "打印任务已提交..."; 
            }
            catch (Exception ex) { MessageBox.Show($"打印失败: {ex.Message}"); }
        }
    }

    private void PreRenderForPrint()
    {
        _renderedPrintBitmap?.Dispose();
        _renderedPrintBitmap = null;

        var originalImage = canvas.GetOriginalImage();
        if (originalImage == null) return;

        var ps = printDocument.DefaultPageSettings;
        float marginW_inch = (ps.Bounds.Width - ps.Margins.Left - ps.Margins.Right) / 100f;
        float marginH_inch = (ps.Bounds.Height - ps.Margins.Top - ps.Margins.Bottom) / 100f;

        int realDpiX = ps.PrinterResolution.X > 0 ? ps.PrinterResolution.X : 600;
        int realDpiY = ps.PrinterResolution.Y > 0 ? ps.PrinterResolution.Y : 600;

        int pxW = (int)(marginW_inch * realDpiX);
        int pxH = (int)(marginH_inch * realDpiY);

        if (pxW > 4000 || pxH > 4000)
        {
            float ratio = Math.Min(4000f / pxW, 4000f / pxH);
            pxW = (int)(pxW * ratio); pxH = (int)(pxH * ratio);
        }

        if (pxW <= 0 || pxH <= 0) return;

        var bitmap = new Bitmap(pxW, pxH, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(_theme.GetPaperBgColor());
        
        canvas.RenderToTarget(g, new RectangleF(0, 0, pxW, pxH));

        string debugPath = @"D:\Documents\my\LMaxPrint\debug_print.png";
        try { bitmap.Save(debugPath, ImageFormat.Png); } catch { }

        _renderedPrintBitmap = bitmap;
    }

    private void PrintDocument_PrintPage(object sender, PrintPageEventArgs e)
    {
        if (_renderedPrintBitmap == null || e.Graphics == null) return;

        RectangleF destRect = e.MarginBounds;
        RectangleF srcRect = new RectangleF(0, 0, _renderedPrintBitmap.Width, _renderedPrintBitmap.Height);
        
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(_renderedPrintBitmap, destRect, srcRect, GraphicsUnit.Pixel);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (this.WindowState == FormWindowState.Normal)
        {
            _theme.WindowWidth = this.Width;
            _theme.WindowHeight = this.Height;
        }
        else
        {
            _theme.WindowWidth = this.RestoreBounds.Width;
            _theme.WindowHeight = this.RestoreBounds.Height;
        }
        _theme.Save();
        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.O: OnImportClick(this, EventArgs.Empty); return true;
            case Keys.Control | Keys.P: OnPrintClick(this, EventArgs.Empty); return true;
            case Keys.Control | Keys.S: OnSaveClick(this, EventArgs.Empty); return true;
            case Keys.Left: canvas.Rotate(-90); return true;
            case Keys.Right: canvas.Rotate(90); return true;
            case Keys.R: canvas.ResetTransform(); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}