using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    /// <summary>
    /// Sélecteur de langue compact : drapeau + code dans l'en-tête, et menu
    /// déroulant avec les vrais drapeaux et les noms natifs des langues.
    /// </summary>
    internal sealed class LanguageSelector : Control, IThemeAware
    {
        private const int FlagWidth = 24;
        private const int FlagHeight = 16;

        private readonly List<Image> _flags = new List<Image>();
        private AppLanguage _selected = AppLanguage.French;
        private bool _hover;
        private bool _open;
        private ContextMenuStrip _menu;

        public event EventHandler SelectedLanguageChanged;

        public LanguageSelector()
        {
            SetStyle(ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable, true);
            BackColor = Theme.Surface;
            Font = Theme.Body;
            Cursor = Cursors.Hand;
            TabStop = true;
            Height = Theme.S(34);
            LoadFlags();
        }

        public AppLanguage SelectedLanguage
        {
            get { return _selected; }
            set
            {
                if (_selected == value)
                {
                    return;
                }
                _selected = value;
                Invalidate();
                if (SelectedLanguageChanged != null)
                {
                    SelectedLanguageChanged(this, EventArgs.Empty);
                }
            }
        }

        public void SetSelectedSilent(AppLanguage language)
        {
            _selected = language;
            Invalidate();
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Surface;
            if (_menu != null)
            {
                StyleMenu();
            }
            Invalidate();
        }

        private static string FlagCode(AppLanguage language)
        {
            switch (language)
            {
                case AppLanguage.English: return "gb";
                case AppLanguage.German: return "de";
                case AppLanguage.Spanish: return "es";
                default: return "fr";
            }
        }

        private static string Code(AppLanguage language)
        {
            switch (language)
            {
                case AppLanguage.English: return "EN";
                case AppLanguage.German: return "DE";
                case AppLanguage.Spanish: return "ES";
                default: return "FR";
            }
        }

        private void LoadFlags()
        {
            AppLanguage[] languages = new AppLanguage[]
            {
                AppLanguage.French,
                AppLanguage.English,
                AppLanguage.German,
                AppLanguage.Spanish
            };
            for (int i = 0; i < languages.Length; i++)
            {
                _flags.Add(LoadFlag(FlagCode(languages[i])));
            }
        }

        private static Image LoadFlag(string code)
        {
            try
            {
                string resource = "EasyIpSwitch.Flag." + code + ".png";
                using (Stream stream = typeof(LanguageSelector).Assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null)
                    {
                        return null;
                    }
                    using (Image source = Image.FromStream(stream))
                    {
                        Bitmap result = new Bitmap(FlagWidth, FlagHeight);
                        using (Graphics g = Graphics.FromImage(result))
                        {
                            g.Clear(Color.Transparent);
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            float scale = Math.Min(
                                FlagWidth / (float)source.Width,
                                FlagHeight / (float)source.Height);
                            int width = Math.Max(1, (int)Math.Round(source.Width * scale));
                            int height = Math.Max(1, (int)Math.Round(source.Height * scale));
                            g.DrawImage(source, new Rectangle(
                                (FlagWidth - width) / 2,
                                (FlagHeight - height) / 2,
                                width, height));
                        }
                        return result;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        internal Image FlagImage(AppLanguage language)
        {
            return FlagFor(language);
        }

        private Image FlagFor(AppLanguage language)
        {
            int index = (int)language;
            if (index < 0 || index >= _flags.Count)
            {
                return null;
            }
            return _flags[index];
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Surface);

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundedRect(rect, Theme.S(9)))
            {
                using (SolidBrush brush = new SolidBrush(_hover || _open ? Theme.SurfaceMuted : Theme.Surface))
                {
                    g.FillPath(brush, path);
                }
                using (Pen pen = new Pen(_hover || _open ? Theme.BorderStrong : Theme.Border, 1f))
                {
                    g.DrawPath(pen, path);
                }
            }

            int pad = Theme.S(9);
            Image flag = FlagFor(_selected);
            int flagLeft = pad;
            if (flag != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(flag, new Rectangle(flagLeft, (Height - FlagHeight) / 2, FlagWidth, FlagHeight));
            }

            int textLeft = flagLeft + (flag != null ? FlagWidth + Theme.S(7) : 0);
            int arrow = Theme.S(22);
            Rectangle textRect = new Rectangle(textLeft, 0,
                Math.Max(0, Width - textLeft - arrow - Theme.S(4)), Height);
            TextRenderer.DrawText(g, Code(_selected), Font, textRect, Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            Rectangle chevron = new Rectangle(Width - arrow, (Height - Theme.S(14)) / 2,
                Theme.S(14), Theme.S(14));
            Glyphs.ChevronDown(g, chevron, Theme.TextMuted, Theme.S(1.6f));
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || e.Button != MouseButtons.Left)
            {
                return;
            }
            Focus();
            if (_open)
            {
                CloseMenu();
            }
            else
            {
                OpenMenu();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!Enabled)
            {
                return;
            }
            if (e.KeyCode == Keys.Down && (int)_selected < (int)AppLanguage.Spanish)
            {
                SelectedLanguage = (AppLanguage)((int)_selected + 1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up && (int)_selected > (int)AppLanguage.French)
            {
                SelectedLanguage = (AppLanguage)((int)_selected - 1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Space || (e.KeyCode == Keys.Enter && !_open))
            {
                OpenMenu();
                e.Handled = true;
            }
        }

        private void OpenMenu()
        {
            CloseMenu();

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = true;
            menu.ShowCheckMargin = false;
            menu.Padding = new Padding(0, Theme.S(6), 0, Theme.S(6));
            menu.Font = Font;
            menu.BackColor = Theme.Surface;
            menu.ForeColor = Theme.Text;
            menu.DropShadowEnabled = true;

            AppLanguage[] languages = new AppLanguage[]
            {
                AppLanguage.French,
                AppLanguage.English,
                AppLanguage.German,
                AppLanguage.Spanish
            };
            for (int i = 0; i < languages.Length; i++)
            {
                AppLanguage language = languages[i];
                ToolStripMenuItem item = new ToolStripMenuItem(L.DisplayName(language));
                item.AutoSize = false;
                item.Height = Theme.S(30);
                item.Width = Math.Max(Theme.S(190), Width + Theme.S(110));
                item.Padding = new Padding(Theme.S(6), 0, Theme.S(10), 0);
                item.BackColor = Theme.Surface;
                item.ForeColor = language == _selected ? Theme.Accent : Theme.Text;
                item.Font = language == _selected ? Theme.BodyBold : Theme.Body;
                item.Image = FlagFor(language);
                item.ImageScaling = ToolStripItemImageScaling.None;
                item.ImageAlign = ContentAlignment.MiddleLeft;
                item.Click += delegate
                {
                    SelectedLanguage = language;
                };
                menu.Items.Add(item);
            }

            menu.Closed += delegate
            {
                _open = false;
                _menu = null;
                Invalidate();
            };

            _menu = menu;
            _open = true;
            StyleMenu();
            menu.Show(this, new Point(0, Height + Theme.S(4)));
            Invalidate();
        }

        private void StyleMenu()
        {
            if (_menu == null)
            {
                return;
            }
            _menu.BackColor = Theme.Surface;
            _menu.ForeColor = Theme.Text;
            _menu.Renderer = new MenuRenderer();
        }

        private void CloseMenu()
        {
            if (_menu != null)
            {
                _menu.Close();
                _menu = null;
            }
            _open = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CloseMenu();
                for (int i = 0; i < _flags.Count; i++)
                {
                    if (_flags[i] != null)
                    {
                        _flags[i].Dispose();
                    }
                }
                _flags.Clear();
            }
            base.Dispose(disposing);
        }

        private sealed class MenuRenderer : ToolStripProfessionalRenderer
        {
            public MenuRenderer()
                : base(new MenuColors())
            {
                RoundedEdges = false;
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Border, 1f))
                {
                    Rectangle rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                    e.Graphics.DrawRectangle(pen, rect);
                }
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                Rectangle rect = new Rectangle(0, 0, e.Item.Width, e.Item.Height);
                if (e.Item.Selected && e.Item.Enabled)
                {
                    using (SolidBrush brush = new SolidBrush(Theme.AccentSoft))
                    {
                        e.Graphics.FillRectangle(brush, rect);
                    }
                }
            }
        }

        private sealed class MenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Theme.Surface; } }
            public override Color ImageMarginGradientBegin { get { return Theme.Surface; } }
            public override Color ImageMarginGradientMiddle { get { return Theme.Surface; } }
            public override Color ImageMarginGradientEnd { get { return Theme.Surface; } }
            public override Color MenuBorder { get { return Theme.Border; } }
            public override Color MenuItemBorder { get { return Theme.AccentSoft; } }
            public override Color MenuItemSelected { get { return Theme.AccentSoft; } }
            public override Color MenuItemSelectedGradientBegin { get { return Theme.AccentSoft; } }
            public override Color MenuItemSelectedGradientEnd { get { return Theme.AccentSoft; } }
            public override Color SeparatorDark { get { return Theme.Border; } }
            public override Color SeparatorLight { get { return Theme.Border; } }
        }
    }
}
