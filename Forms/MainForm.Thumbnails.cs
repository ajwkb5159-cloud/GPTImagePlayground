namespace ImageGenerator.Forms;

internal partial class MainForm
{
    private void AddThumbnail(string imagePath)
    {
        var thumb = CreateThumbnail(imagePath, ScaleValue(40));
        thumb.Cursor = Cursors.Hand;
        thumb.Tag = imagePath;
        thumb.Click += (_, _) =>
        {
            RemoveThumbnail(thumb);
            _promptBox.Focus();
        };
        _thumbnailStrip.Visible = true;
        _thumbnailStrip.Controls.Add(thumb);
        UpdateInputPanelHeight();
    }

    private void RemoveLastThumbnail()
    {
        for (var index = _thumbnailStrip.Controls.Count - 1; index >= 0; index--)
        {
            if (_thumbnailStrip.Controls[index] is PictureBox thumb)
            {
                RemoveThumbnail(thumb);
                return;
            }
        }
    }

    private void RemoveThumbnail(PictureBox thumb)
    {
        if (thumb.Tag is string imagePath)
        {
            _attachedImages.Remove(imagePath);
        }
        else
        {
            var index = _thumbnailStrip.Controls.IndexOf(thumb);
            if (index >= 0 && index < _attachedImages.Count)
                _attachedImages.RemoveAt(index);
        }

        _thumbnailStrip.Controls.Remove(thumb);
        DisposeThumbnail(thumb);
        _thumbnailStrip.Visible = _thumbnailStrip.Controls.Count > 0;
        UpdateInputPanelHeight();
    }

    private void ClearAttachedThumbnails()
    {
        while (_thumbnailStrip.Controls.Count > 0)
        {
            var control = _thumbnailStrip.Controls[0];
            _thumbnailStrip.Controls.RemoveAt(0);

            if (control is PictureBox thumb)
                DisposeThumbnail(thumb);
            else
                control.Dispose();
        }

        _attachedImages.Clear();
        _thumbnailStrip.Visible = false;
        UpdateInputPanelHeight();
    }

    private static void DisposeThumbnail(PictureBox thumb)
    {
        var image = thumb.Image;
        thumb.Image = null;
        image?.Dispose();
        thumb.Dispose();
    }

    private PictureBox CreateThumbnail(string imagePath, int size)
    {
        var pb = new PictureBox
        {
            Size = new Size(size, size),
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle,
        };

        try
        {
            pb.Image = GetCachedThumbnail(imagePath, size);
        }
        catch
        {
            pb.BackColor = Color.LightGray;
        }

        return pb;
    }

    private Image GetCachedThumbnail(string imagePath, int size)
    {
        var key = CreateThumbnailCacheKey(imagePath, size);
        if (_thumbnailCache.TryGetValue(key, out var cached))
        {
            _thumbnailLru.Remove(cached.Node);
            _thumbnailLru.AddFirst(cached.Node);
            return new Bitmap(cached.Image);
        }

        using var source = LoadImageFromBytes(File.ReadAllBytes(imagePath));
        var thumbnail = CreateThumbnailImage(source, Math.Max(1, size));
        var node = _thumbnailLru.AddFirst(key);
        _thumbnailCache[key] = new ThumbnailCacheEntry
        {
            Image = thumbnail,
            Node = node,
        };
        TrimThumbnailCache();
        return new Bitmap(thumbnail);
    }

    private static string CreateThumbnailCacheKey(string imagePath, int size)
    {
        var fullPath = Path.GetFullPath(imagePath);
        var info = new FileInfo(fullPath);
        return $"{fullPath}|{size}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
    }

    private static Image CreateThumbnailImage(Image source, int size)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

        var scale = Math.Min((double)size / source.Width, (double)size / source.Height);
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var left = (size - width) / 2;
        var top = (size - height) / 2;
        graphics.DrawImage(source, new Rectangle(left, top, width, height));
        return bitmap;
    }

    private void TrimThumbnailCache()
    {
        while (_thumbnailCache.Count > MaxThumbnailCacheEntries && _thumbnailLru.Last != null)
        {
            var key = _thumbnailLru.Last.Value;
            _thumbnailLru.RemoveLast();
            if (_thumbnailCache.Remove(key, out var entry))
                entry.Image.Dispose();
        }
    }

    private void ClearThumbnailCache()
    {
        foreach (var entry in _thumbnailCache.Values)
            entry.Image.Dispose();

        _thumbnailCache.Clear();
        _thumbnailLru.Clear();
    }
}
