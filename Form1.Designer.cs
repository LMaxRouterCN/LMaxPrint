namespace LMaxPrint;

partial class Form1
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 释放设计器生成的组件
            if (components != null)
            {
                components.Dispose();
            }
            
            // 释放降维打击产生的预渲染位图，防止内存泄漏
            _renderedPrintBitmap?.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(1024, 768);
        this.Text = "LMaxPrint - 轻量级图像打印工具";
        this.StartPosition = FormStartPosition.CenterScreen;
    }

    #endregion
}