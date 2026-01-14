using System;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.UI.Controls;
using ArdysaModsTools.UI;

namespace ArdysaModsTools.UI.Controls.Widgets
{
    public class TileCard : UserControl
    {
        public string Id { get; private set; } = string.Empty;
        public Label Caption { get; private set; } = null!;
        public Control TileVisual { get; private set; } = null!;
        public bool Selected { get; private set; }
        private PictureBox? _thumbnail;
        private string? _currentThumbnailUrl;

        public event EventHandler? Clicked;

        public TileCard()
        {
            Initialize();
        }

        private void Initialize()
        {
            DoubleBuffered = true;

            Width = Theme.TileSize;
            Height = Theme.TileSize + Theme.TileCaptionHeight + Theme.TileMarginVertical * 2;

            Margin = new Padding(Theme.TileMarginHorizontal / 2, Theme.TileMarginVertical / 2,
                                 Theme.TileMarginHorizontal / 2, Theme.TileMarginVertical / 2);

            // visual area (rounded)
            try
            {
                TileVisual = new RoundedPanel
                {
                    Size = new Size(Theme.TileSize, Theme.TileSize),
                    BorderRadius = 14,
                    BackColor = Theme.TileBackground,
                    Location = new Point(0, 0)
                };
            }
            catch
            {
                TileVisual = new Panel
                {
                    Size = new Size(Theme.TileSize, Theme.TileSize),
                    BackColor = Theme.TileBackground,
                    Location = new Point(0, 0)
                };
            }

            TileVisual.Cursor = Cursors.Hand;
            TileVisual.Click += (s, e) => OnClicked();
            TileVisual.MouseEnter += (s, e) => { TrySetHover(true); };
            TileVisual.MouseLeave += (s, e) => { TrySetHover(false); };

            Caption = new Label
            {
                AutoSize = false,
                Size = new Size(Theme.TileSize, Theme.TileCaptionHeight),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, Theme.TileSize + 6),
                ForeColor = Theme.TextLight,
                Font = Theme.TileCaptionFont,
                Cursor = Cursors.Hand
            };
            Caption.Click += (s, e) => OnClicked();
            Caption.MouseEnter += (s, e) => { TrySetHover(true); };
            Caption.MouseLeave += (s, e) => { TrySetHover(false); };

            Controls.Add(TileVisual);
            Controls.Add(Caption);
        }

        private void TrySetHover(bool hover)
        {
            try
            {
                if (!Selected)
                {
                    if (TileVisual is RoundedPanel rp)
                    {
                        rp.BackColor = hover ? Theme.TileHover : Theme.TileBackground;
                        rp.BorderThickness = hover ? 2 : 0;
                        rp.BorderColor = hover ? Theme.TileHoverBorder : Color.Transparent;
                    }
                    else
                    {
                        TileVisual.BackColor = hover ? Theme.TileHover : Theme.TileBackground;
                    }
                }
            }
            catch (Exception ex)
            {
                ArdysaModsTools.Core.Services.FallbackLogger.Log($"TileCard.TrySetHover error: {ex.Message}");
            }
        }

        /// <summary>
        /// Adjust internal sizes so tile can be resized at runtime (used by HeroRow to enforce max columns).
        /// </summary>
        public void SetTileSize(int tileSize)
        {
            if (tileSize <= 0) return;

            this.Width = tileSize;
            this.Height = tileSize + Theme.TileCaptionHeight + Theme.TileMarginVertical * 2;

            try
            {
                if (TileVisual != null) TileVisual.Size = new Size(tileSize, tileSize);
                if (Caption != null)
                {
                    Caption.Size = new Size(tileSize, Theme.TileCaptionHeight);
                    Caption.Location = new Point(0, tileSize + 6);
                }
            }
            catch (Exception ex)
            {
                ArdysaModsTools.Core.Services.FallbackLogger.Log($"TileCard.SetTileSize error: {ex.Message}");
            }
        }

        public void SetData(string id, string caption)
        {
            Id = id ?? string.Empty;
            Caption.Text = caption ?? string.Empty;
            UpdateVisual();
        }

        public void SetSelected(bool sel)
        {
            Selected = sel;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            try
            {
                if (TileVisual is RoundedPanel rp)
                {
                    if (Selected)
                    {
                        // Selected: subtle tinted background + accent border
                        rp.BackColor = Theme.TileSelectedBg;
                        rp.BorderThickness = 3;
                        rp.BorderColor = Theme.TileSelected;
                    }
                    else
                    {
                        // Not selected: normal background, no border
                        rp.BackColor = Theme.TileBackground;
                        rp.BorderThickness = 0;
                        rp.BorderColor = Color.Transparent;
                    }
                }
                else if (TileVisual != null)
                {
                    TileVisual.BackColor = Selected ? Theme.TileSelectedBg : Theme.TileBackground;
                }
                
                // Caption: accent color when selected, light text otherwise
                Caption.ForeColor = Selected ? Theme.Accent : Theme.TextLight;
                
                // Update thumbnail bounds so border is visible
                UpdateThumbnailBounds();
            }
            catch (Exception ex)
            {
                ArdysaModsTools.Core.Services.FallbackLogger.Log($"TileCard.UpdateVisual error: {ex.Message}");
            }
        }

        /// <summary>
        /// Position thumbnail with margin so border is visible around it.
        /// </summary>
        private void UpdateThumbnailBounds()
        {
            if (_thumbnail == null || TileVisual == null) return;
            
            // When selected, add more margin for thicker border
            int margin = Selected ? 6 : 4;
            _thumbnail.Location = new Point(margin, margin);
            _thumbnail.Size = new Size(
                TileVisual.Width - margin * 2,
                TileVisual.Height - margin * 2
            );
            _thumbnail.BringToFront();
        }

        private void OnClicked()
        {
            Clicked?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Load and display a thumbnail image from a URL.
        /// </summary>
        public void SetThumbnail(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                // No thumbnail - just show colored tile
                if (_thumbnail != null)
                {
                    _thumbnail.Image?.Dispose();
                    _thumbnail.Image = null;
                }
                return;
            }

            // Skip if same URL already loading/loaded
            if (_currentThumbnailUrl == imageUrl) return;
            _currentThumbnailUrl = imageUrl;

            // Create PictureBox if needed
            if (_thumbnail == null)
            {
                _thumbnail = new PictureBox
                {
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Transparent
                };
                // Add margin so border is visible (not Dock.Fill which covers everything)
                _thumbnail.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
                _thumbnail.Click += (s, e) => OnClicked();
                _thumbnail.Cursor = Cursors.Hand;
                _thumbnail.MouseEnter += (s, e) => { TrySetHover(true); };
                _thumbnail.MouseLeave += (s, e) => { TrySetHover(false); };
                TileVisual.Controls.Add(_thumbnail);
                // Position with margin for border
                UpdateThumbnailBounds();
            }

            // Load async
            _ = LoadThumbnailAsync(imageUrl);
        }

        private async Task LoadThumbnailAsync(string url)
        {
            try
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"TileCard: Loading thumbnail from {url}");
#endif
                
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(15);
                var bytes = await client.GetByteArrayAsync(url).ConfigureAwait(false);

#if DEBUG
                System.Diagnostics.Debug.WriteLine($"TileCard: Downloaded {bytes.Length} bytes");
#endif

                // Create image from bytes - DON'T dispose the MemoryStream
                var ms = new System.IO.MemoryStream(bytes);
                var img = Image.FromStream(ms);

                // Update on UI thread - use different approach based on handle status
                ApplyThumbnailImage(img);
            }
            catch (Exception ex)
            {
                // Silently fail - thumbnail loading is non-critical
                ArdysaModsTools.Core.Services.FallbackLogger.Log($"TileCard.LoadThumbnailAsync failed for {url}: {ex.Message}");
            }
        }

        private void ApplyThumbnailImage(Image img)
        {
            void SetImage()
            {
                try
                {
                    if (_thumbnail != null && !_thumbnail.IsDisposed)
                    {
                        _thumbnail.Image?.Dispose();
                        _thumbnail.Image = img;
                        _thumbnail.BringToFront();
                        _thumbnail.Visible = true;
                        TileVisual.Invalidate();
#if DEBUG
                        System.Diagnostics.Debug.WriteLine($"TileCard: Thumbnail displayed successfully");
#endif
                    }
                }
                catch
                {
                    // Silently ignore - thumbnail display is non-critical
                }
            }

            try
            {
                if (InvokeRequired)
                {
                    if (IsHandleCreated && !IsDisposed)
                    {
                        BeginInvoke(new Action(SetImage));
                    }
                    else
                    {
                        // Handle not yet created - store image for later
#if DEBUG
                        System.Diagnostics.Debug.WriteLine($"TileCard: Handle not ready, storing image for later");
#endif
                        _pendingImage = img;
                        HandleCreated += OnHandleCreatedSetImage;
                    }
                }
                else
                {
                    // Already on UI thread
                    SetImage();
                }
            }
            catch
            {
                // Silently ignore - thumbnail display is non-critical
            }
        }

        private Image? _pendingImage;

        private void OnHandleCreatedSetImage(object? sender, EventArgs e)
        {
            HandleCreated -= OnHandleCreatedSetImage;
            if (_pendingImage != null)
            {
                ApplyThumbnailImage(_pendingImage);
                _pendingImage = null;
            }
        }
    }
}
