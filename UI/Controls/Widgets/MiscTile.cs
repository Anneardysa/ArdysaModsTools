using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using SixLabors.ImageSharp.PixelFormats;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace ArdysaModsTools.UI.Controls.Widgets
{
    /// <summary>
    /// Selectable tile for a misc option choice with 150px thumbnail.
    /// </summary>
    public class MiscTile : UserControl
    {
        private readonly Panel _tilePanel;
        private readonly Label _caption;
        private readonly PictureBox _thumbnail;
        private bool _selected;
        private string? _currentThumbnailUrl;

        public string ChoiceId { get; private set; } = string.Empty;
        public bool Selected => _selected;

        public event EventHandler? Clicked;

        // 150px square tile with caption below
        private const int TileSize = 150;
        private const int CaptionHeight = 28;
        private const int BorderRadius = 12;

        public MiscTile()
        {
            Width = TileSize;
            Height = TileSize + CaptionHeight;
            Margin = new Padding(8, 6, 8, 6);
            BackColor = Color.Transparent;
            DoubleBuffered = true;
            Cursor = Cursors.Hand;

            // Tile background panel (150x150)
            _tilePanel = new Panel
            {
                Size = new Size(TileSize, TileSize),
                Location = new Point(0, 0),
                BackColor = Theme.TileBackground,
                Cursor = Cursors.Hand
            };
            _tilePanel.Paint += PaintTileBackground;

            // Thumbnail image
            _thumbnail = new PictureBox
            {
                Size = new Size(TileSize - 12, TileSize - 12),
                Location = new Point(6, 6),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            _thumbnail.Click += (s, e) => OnClicked();
            _thumbnail.MouseEnter += (s, e) => SetHover(true);
            _thumbnail.MouseLeave += (s, e) => SetHover(false);
            _tilePanel.Controls.Add(_thumbnail);

            // Caption label below tile
            _caption = new Label
            {
                AutoSize = false,
                Size = new Size(TileSize, CaptionHeight),
                Location = new Point(0, TileSize + 2),
                TextAlign = ContentAlignment.TopCenter,
                ForeColor = Theme.TextLight,
                Font = new Font("JetBrains Mono", 9F),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            Controls.Add(_tilePanel);
            Controls.Add(_caption);

            // Wire events
            _tilePanel.Click += (s, e) => OnClicked();
            _caption.Click += (s, e) => OnClicked();
            _tilePanel.MouseEnter += (s, e) => SetHover(true);
            _tilePanel.MouseLeave += (s, e) => SetHover(false);
            _caption.MouseEnter += (s, e) => SetHover(true);
            _caption.MouseLeave += (s, e) => SetHover(false);
        }

        public void SetData(string choiceId, string displayText)
        {
            ChoiceId = choiceId;
            _caption.Text = displayText;
        }

        public void SetThumbnail(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                _thumbnail.Image?.Dispose();
                _thumbnail.Image = null;
                return;
            }

            if (_currentThumbnailUrl == imageUrl) return;
            _currentThumbnailUrl = imageUrl;

            _ = LoadThumbnailAsync(imageUrl);
        }

        private async Task LoadThumbnailAsync(string url)
        {
            // Try multiple formats: original, then alternatives
            var formatsToTry = GetFormatVariants(url);
            
            foreach (var formatUrl in formatsToTry)
            {
                try
                {
                    using var client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(10);
                    var bytes = await client.GetByteArrayAsync(formatUrl).ConfigureAwait(false);

                    // Use ImageSharp for WebP and other formats, then convert to GDI+ Bitmap
                    var img = ConvertToGdiBitmap(bytes);
                    if (img != null)
                    {
                        ApplyThumbnailImage(img);
                        return; // Success, exit loop
                    }
                }
                catch
                {
                    // Try next format
                    continue;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"MiscTile: Failed to load thumbnail from any format: {url}");
        }

        /// <summary>
        /// Convert image bytes to GDI+ Bitmap using ImageSharp (supports WebP, PNG, JPEG, etc.)
        /// </summary>
        private static System.Drawing.Image? ConvertToGdiBitmap(byte[] bytes)
        {
            try
            {
                using var imageSharp = ImageSharpImage.Load<Rgba32>(bytes);
                
                // Create GDI+ bitmap
                var bitmap = new Bitmap(imageSharp.Width, imageSharp.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                
                // Copy pixels efficiently using LockBits
                var bitmapData = bitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                
                try
                {
                    imageSharp.ProcessPixelRows(accessor =>
                    {
                        unsafe
                        {
                            for (int y = 0; y < accessor.Height; y++)
                            {
                                var pixelRow = accessor.GetRowSpan(y);
                                byte* destRow = (byte*)bitmapData.Scan0 + (y * bitmapData.Stride);
                                
                                for (int x = 0; x < accessor.Width; x++)
                                {
                                    var pixel = pixelRow[x];
                                    int offset = x * 4;
                                    destRow[offset] = pixel.B;     // Blue
                                    destRow[offset + 1] = pixel.G; // Green
                                    destRow[offset + 2] = pixel.R; // Red
                                    destRow[offset + 3] = pixel.A; // Alpha
                                }
                            }
                        }
                    });
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }
                
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MiscTile: ImageSharp conversion failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get URL variants with different image formats to try.
        /// </summary>
        private static string[] GetFormatVariants(string url)
        {
            // If URL already has extension, create variants
            if (url.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                var baseUrl = url[..^4]; // Remove .png
                return new[] { url, baseUrl + ".webp", baseUrl + ".jpg" };
            }
            else if (url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                var baseUrl = url[..^5]; // Remove .webp
                return new[] { url, baseUrl + ".png", baseUrl + ".jpg" };
            }
            else if (url.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                     url.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                var ext = url.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ? 5 : 4;
                var baseUrl = url[..^ext];
                return new[] { url, baseUrl + ".png", baseUrl + ".webp" };
            }
            
            // No recognized extension, try common formats
            return new[] { url + ".png", url + ".webp", url + ".jpg", url };
        }

        private void ApplyThumbnailImage(Image img)
        {
            void SetImage()
            {
                if (_thumbnail != null && !_thumbnail.IsDisposed)
                {
                    _thumbnail.Image?.Dispose();
                    _thumbnail.Image = img;
                    _thumbnail.Visible = true;
                }
            }

            try
            {
                if (InvokeRequired && IsHandleCreated && !IsDisposed)
                {
                    BeginInvoke(new Action(SetImage));
                }
                else
                {
                    SetImage();
                }
            }
            catch { /* Ignore if form is closing */ }
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            UpdateVisual();
        }

        private void SetHover(bool hover)
        {
            if (!_selected)
            {
                _tilePanel.BackColor = hover ? Theme.TileHover : Theme.TileBackground;
                _tilePanel.Invalidate();
            }
        }

        private void UpdateVisual()
        {
            if (_selected)
            {
                _tilePanel.BackColor = Theme.TileSelectedBg;
                _caption.ForeColor = Theme.Accent;
            }
            else
            {
                _tilePanel.BackColor = Theme.TileBackground;
                _caption.ForeColor = Theme.TextLight;
            }

            // Adjust thumbnail bounds for border
            int margin = _selected ? 8 : 6;
            _thumbnail.Location = new Point(margin, margin);
            _thumbnail.Size = new Size(TileSize - margin * 2, TileSize - margin * 2);

            _tilePanel.Invalidate();
        }

        private void OnClicked()
        {
            Clicked?.Invoke(this, EventArgs.Empty);
        }

        private void PaintTileBackground(object? sender, PaintEventArgs e)
        {
            var panel = sender as Panel;
            if (panel == null) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            
            // Use parent's background for transparency effect
            var parentBg = Parent?.BackColor ?? Theme.RowBackground;
            e.Graphics.Clear(parentBg);

            var rect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
            using var path = CreateRoundedRectPath(rect, BorderRadius);

            // Fill background
            using var brush = new SolidBrush(panel.BackColor);
            e.Graphics.FillPath(brush, path);

            // Draw border if selected
            if (_selected)
            {
                using var pen = new Pen(Theme.Accent, 3);
                e.Graphics.DrawPath(pen, path);
            }
        }

        private static GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            return path;
        }
    }
}
