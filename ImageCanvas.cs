using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.IO;

namespace LMaxPrint
{
    public partial class ImageCanvas : Control
    {
        private Image? _image;
        private ThemeSettings _theme = new ThemeSettings();
        
        private float _worldImgX = 0f;             
        private float _worldImgY = 0f;             
        private float _imgZoom = 1.0f;       
        private float _imgRotation = 0f;    
        private bool _isMirroredX = false;    
        private bool _isMirroredY = false;    

        private float _viewZoom = 1.0f;      
        private float _viewPanX = 0f;
        private float _viewPanY = 0f;

        private PointF _lastMousePos;         
        private bool _isDraggingImage; 
        private bool _isPanningView;

        private float _pageW = 827f; 
        private float _pageH = 1169f;
        private float _marginTop = 0f, _marginBottom = 0f, _marginLeft = 0f, _marginRight = 0f;
        private RectangleF _printRect; 

        public float MinZoom { get; set; } = 0.01f;
        public float MaxZoom { get; set; } = 20.0f;
        public float ZoomStep { get; set; } = 0.1f;

        public event EventHandler? ImageChanged;
        public event EventHandler? TransformChanged;
        public event EventHandler<string>? PhysicalSizeChanged;

        public ImageCanvas()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            
            this.MouseDown += OnMouseDown;
            this.MouseMove += OnMouseMove;
            this.MouseUp += OnMouseUp;
            this.MouseWheel += OnMouseWheel;
            this.Resize += (s, e) => this.Invalidate();
            this.AllowDrop = true;
            this.DragEnter += OnDragEnter;
            this.DragDrop += OnDragDrop;
        }

        public void ApplyTheme(ThemeSettings theme)
        {
            _theme = theme;
            this.Invalidate();
        }

        public void SetPageInfo(float widthInch, float heightInch, float mTop, float mBottom, float mLeft, float mRight)
        {
            _pageW = widthInch * 100f; 
            _pageH = heightInch * 100f;
            _marginTop = mTop * 100f; _marginBottom = mBottom * 100f; 
            _marginLeft = mLeft * 100f; _marginRight = mRight * 100f;

            _printRect = new RectangleF(
                -_pageW / 2f + _marginLeft, 
                -_pageH / 2f + _marginTop, 
                _pageW - _marginLeft - _marginRight, 
                _pageH - _marginTop - _marginBottom
            );

            FitToPrintArea(); 
            this.Invalidate();
        }

        public void LoadImage(string filePath)
        {
            try
            {
                _image?.Dispose();
                byte[] imageBytes = File.ReadAllBytes(filePath);
                using var ms = new MemoryStream(imageBytes);
                using var tempImg = Image.FromStream(ms);
                
                int orientationId = 0x0112; 
                if (tempImg.PropertyIdList.Contains(orientationId))
                {
                    var prop = tempImg.GetPropertyItem(orientationId);
                    if (prop?.Value != null && prop.Value.Length > 0)
                    {
                        int orientation = prop.Value[0];
                        switch (orientation)
                        {
                            case 2: tempImg.RotateFlip(RotateFlipType.RotateNoneFlipX); break;
                            case 3: tempImg.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
                            case 4: tempImg.RotateFlip(RotateFlipType.RotateNoneFlipY); break;
                            case 5: tempImg.RotateFlip(RotateFlipType.Rotate90FlipX); break;
                            case 6: tempImg.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
                            case 7: tempImg.RotateFlip(RotateFlipType.Rotate270FlipX); break;
                            case 8: tempImg.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
                        }
                        tempImg.RemovePropertyItem(orientationId);
                    }
                }

                _image = new Bitmap(tempImg.Width, tempImg.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(_image)) 
                { 
                    g.Clear(Color.Transparent); 
                    g.DrawImage(tempImg, 0, 0, tempImg.Width, tempImg.Height); 
                }
                
                FitToPrintArea(); 
                ImageChanged?.Invoke(this, EventArgs.Empty); 
                this.Invalidate(); 
                this.Focus();
            }
            catch (Exception ex) { MessageBox.Show($"加载图像失败: {ex.Message}"); }
        }

        private void NotifyPhysicalSize()
        {
            if (_image == null) return;
            float wCm = _image.Width * _imgZoom / 100f * 2.54f;
            float hCm = _image.Height * _imgZoom / 100f * 2.54f;
            PhysicalSizeChanged?.Invoke(this, $"物理尺寸: {wCm:F1} cm × {hCm:F1} cm | 视图: {_viewZoom * 100:F0}%");
        }

        private PointF ScreenToWorld(PointF screenPt) {
            float cx = this.Width / 2f + _viewPanX;
            float cy = this.Height / 2f + _viewPanY;
            return new PointF((screenPt.X - cx) / _viewZoom, (screenPt.Y - cy) / _viewZoom);
        }

        public void RenderToTarget(Graphics g, RectangleF targetRect)
        {
            if (_image == null || _printRect.Width <= 0) return;

            var state = g.Save();

            g.TranslateTransform(targetRect.Width / 2f, targetRect.Height / 2f);
            float scale = targetRect.Width / _printRect.Width;
            g.ScaleTransform(scale, scale);
            
            g.SetClip(_printRect);

            ApplyImageTransform(g);

            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.DrawImage(_image, -_image.Width / 2f, -_image.Height / 2f, _image.Width, _image.Height);

            g.Restore(state);
        }

        private void ApplyImageTransform(Graphics g)
        {
            g.TranslateTransform(_worldImgX, _worldImgY);
            g.RotateTransform(_imgRotation);
            if (_isMirroredX) g.ScaleTransform(-1, 1);
            if (_isMirroredY) g.ScaleTransform(1, -1);
            g.ScaleTransform(_imgZoom, _imgZoom);
        }

        public Image? GetPrintableImage()
        {
            if (_image == null || _printRect.Width <= 0) return null;
            int w = (int)_printRect.Width, h = (int)_printRect.Height;
            if (w <= 0 || h <= 0) return null;
            var bitmap = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(bitmap);
            g.Clear(_theme.GetPaperBgColor());
            RenderToTarget(g, new RectangleF(0, 0, w, h));
            return bitmap;
        }

        public Image? GetOriginalImage() => _image;

        public void FitToPrintArea()
        {
            if (_image == null || _printRect.Width <= 0) return;
            
            _imgZoom = Math.Min(_printRect.Width / _image.Width, _printRect.Height / _image.Height);
            _imgRotation = 0f; _isMirroredX = false; _isMirroredY = false;
            _worldImgX = 0f; _worldImgY = 0f; 
            
            _viewZoom = Math.Min((this.Width - 40) / _pageW, (this.Height - 40) / _pageH);
            _viewPanX = 0f; _viewPanY = 0f;
            
            TransformChanged?.Invoke(this, EventArgs.Empty);
            NotifyPhysicalSize();
            this.Invalidate(); 
        }

        public void ResetTransform() 
        { 
            if (_image == null || _printRect.Width <= 0) return;
            _imgZoom = Math.Min(_printRect.Width / _image.Width, _printRect.Height / _image.Height);
            _imgRotation = 0f; _isMirroredX = false; _isMirroredY = false;
            _worldImgX = 0f; _worldImgY = 0f;
            TransformChanged?.Invoke(this, EventArgs.Empty);
            NotifyPhysicalSize();
            this.Invalidate();
        }

        public void Rotate(float deltaAngle) { _imgRotation = (_imgRotation + deltaAngle) % 360; TransformChanged?.Invoke(this, EventArgs.Empty); this.Invalidate(); }
        public void ToggleMirrorX() { _isMirroredX = !_isMirroredX; TransformChanged?.Invoke(this, EventArgs.Empty); this.Invalidate(); }
        public void ToggleMirrorY() { _isMirroredY = !_isMirroredY; TransformChanged?.Invoke(this, EventArgs.Empty); this.Invalidate(); }

        private void OnMouseDown(object? sender, MouseEventArgs e) 
        { 
            if (e.Button == MouseButtons.Left && _image != null) { 
                _lastMousePos = ScreenToWorld(e.Location); 
                _isDraggingImage = true; 
                this.Cursor = Cursors.Hand; 
                this.Focus(); 
            } 
            else if (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Right) {
                _lastMousePos = e.Location;
                _isPanningView = true;
                this.Cursor = Cursors.NoMove2D;
            }
        }
        
        private void OnMouseMove(object? sender, MouseEventArgs e) 
        { 
            if (_isDraggingImage && _image != null) { 
                var worldPos = ScreenToWorld(e.Location);
                _worldImgX += worldPos.X - _lastMousePos.X; 
                _worldImgY += worldPos.Y - _lastMousePos.Y; 
                _lastMousePos = worldPos; 
                this.Invalidate(); 
            } 
            else if (_isPanningView) {
                _viewPanX += e.X - _lastMousePos.X;
                _viewPanY += e.Y - _lastMousePos.Y;
                _lastMousePos = e.Location;
                this.Invalidate();
            }
        }
        
        private void OnMouseUp(object? sender, MouseEventArgs e) 
        { 
            if (e.Button == MouseButtons.Left) { _isDraggingImage = false; this.Cursor = Cursors.Default; TransformChanged?.Invoke(this, EventArgs.Empty); } 
            else if (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Right) { _isPanningView = false; this.Cursor = Cursors.Default; }
        }
        
        private void OnMouseWheel(object? sender, MouseEventArgs e) 
        { 
            if (_image == null && !ModifierKeys.HasFlag(Keys.Control)) return; 
            
            if (ModifierKeys.HasFlag(Keys.Control)) {
                float factor = e.Delta > 0 ? 1.1f : 0.9f;
                float newZoom = Math.Max(0.1f, Math.Min(5.0f, _viewZoom * factor));
                float actual = newZoom / _viewZoom;
                
                float cx = this.Width / 2f + _viewPanX;
                float cy = this.Height / 2f + _viewPanY;
                _viewPanX = e.X - (e.X - cx) * actual - this.Width / 2f;
                _viewPanY = e.Y - (e.Y - cy) * actual - this.Height / 2f;
                _viewZoom = newZoom;
            } else {
                var anchor = ScreenToWorld(e.Location);
                var newZoom = Math.Max(MinZoom, Math.Min(MaxZoom, _imgZoom * (e.Delta > 0 ? (1 + ZoomStep) : (1 - ZoomStep))));
                float actual = newZoom / _imgZoom;
                
                _worldImgX = anchor.X - (anchor.X - _worldImgX) * actual;
                _worldImgY = anchor.Y - (anchor.Y - _worldImgY) * actual;
                _imgZoom = newZoom;
            }
            NotifyPhysicalSize();
            this.Invalidate(); 
        }

        private void OnDragEnter(object? sender, DragEventArgs e) { if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; }
        private void OnDragDrop(object? sender, DragEventArgs e) { if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)) { var files = (string[]?)e.Data.GetData(DataFormats.FileDrop); if (files != null && files.Length > 0) LoadImage(files[0]); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(_theme.GetCanvasBgColor());

            var state = e.Graphics.Save();
            
            float cx = this.Width / 2f + _viewPanX;
            float cy = this.Height / 2f + _viewPanY;
            e.Graphics.TranslateTransform(cx, cy);
            e.Graphics.ScaleTransform(_viewZoom, _viewZoom);

            // 保存纯粹的视图坐标系状态
            var viewState = e.Graphics.Save();

            using var shadowBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0));
            e.Graphics.FillRectangle(shadowBrush, -_pageW / 2f + 4, -_pageH / 2f + 4, _pageW, _pageH);
            
            using var paperBrush = new SolidBrush(_theme.GetPaperBgColor());
            e.Graphics.FillRectangle(paperBrush, -_pageW / 2f, -_pageH / 2f, _pageW, _pageH);
            
            using var borderPen = new Pen(_theme.GetPaperBorderColor(), 1f); 
            e.Graphics.DrawRectangle(borderPen, -_pageW / 2f, -_pageH / 2f, _pageW, _pageH);
            
            if (_image != null)
            {
                ApplyImageTransform(e.Graphics);
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                e.Graphics.DrawImage(_image, -_image.Width / 2f, -_image.Height / 2f, _image.Width, _image.Height);
            }
            else
            {
                var text = "拖入图片或点击导入 (Ctrl+O)";
                var size = e.Graphics.MeasureString(text, this.Font); 
                using var textBrush = new SolidBrush(Color.Gray);
                e.Graphics.DrawString(text, this.Font, textBrush, -size.Width / 2, -size.Height / 2);
            }

            // 【核心修复】：恢复到纯粹的视图坐标系，确保虚线框置顶且不受图像变换影响！
            e.Graphics.Restore(viewState);
            using var dashPen = new Pen(_theme.GetPrintAreaColor(), _theme.PrintAreaThickness) { DashStyle = DashStyle.Dash }; 
            e.Graphics.DrawRectangle(dashPen, _printRect.X, _printRect.Y, _printRect.Width, _printRect.Height);

            e.Graphics.Restore(state);
        }

        protected override void Dispose(bool disposing) { if (disposing) _image?.Dispose(); base.Dispose(disposing); }
    }
}