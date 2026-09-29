// TaskbarFetch
// Copyright (c) 2026 Thyann Seng
// Licensed under the MIT License. See LICENSE in the repository root.

using System.Drawing;

namespace TaskbarFetch
{
    internal static class WindowGeometry
    {
        internal static Rectangle MapRectangle(Rectangle window, Rectangle sourceWork, Rectangle targetWork)
        {
            if (sourceWork.Width <= 0 || sourceWork.Height <= 0 ||
                targetWork.Width <= 0 || targetWork.Height <= 0)
                return window;

            double xRatio = (double)(window.Left - sourceWork.Left) / sourceWork.Width;
            double yRatio = (double)(window.Top - sourceWork.Top) / sourceWork.Height;
            double widthRatio = (double)window.Width / sourceWork.Width;
            double heightRatio = (double)window.Height / sourceWork.Height;

            int width = (int)System.Math.Round(widthRatio * targetWork.Width);
            int height = (int)System.Math.Round(heightRatio * targetWork.Height);

            int minWidth = System.Math.Min(120, targetWork.Width);
            int minHeight = System.Math.Min(80, targetWork.Height);
            width = System.Math.Max(minWidth, System.Math.Min(width, targetWork.Width));
            height = System.Math.Max(minHeight, System.Math.Min(height, targetWork.Height));

            int left = targetWork.Left + (int)System.Math.Round(xRatio * targetWork.Width);
            int top = targetWork.Top + (int)System.Math.Round(yRatio * targetWork.Height);

            // Keep the window fully reachable on the destination work area.
            if (left < targetWork.Left)
                left = targetWork.Left;
            if (top < targetWork.Top)
                top = targetWork.Top;
            if (left + width > targetWork.Right)
                left = targetWork.Right - width;
            if (top + height > targetWork.Bottom)
                top = targetWork.Bottom - height;

            return new Rectangle(left, top, width, height);
        }
    }
}
