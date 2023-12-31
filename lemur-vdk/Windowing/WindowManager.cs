﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Lemur.GUI
{
    /// <summary>
    /// A WPF Control that contains the logic for moving, minimizing, opening, closing, resizing windows.
    /// </summary>
    public class WindowManager : Canvas
    {
        private Computer computer => Computer.Current;
        private ResizableWindow? targetWindow;
        private static double resizeMargin = 10;
        private ResizeEdge resizingEdge;
        private Point startDragPosition;
        private bool isDragging;
        private bool isResizing;

        // TODO : Add Ctrl + W (Close Window)

        // TODO : in BringToTopOfDesktop(), set targetWindow to last clicked window
        // TODO : bring the rest of the functions here like BringToTopOfDesktop();

        protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
        {
            if (targetWindow != null)
                targetWindow = null;

            isDragging = false;
            isResizing = false;
        }
        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);

            if (targetWindow == null || (!isResizing && !isDragging))
                return;

            foreach (var process in computer.ProcessManager.ProcessClassTable.SelectMany(i => i.Value.Select(p => p)))
                process.UI.ResizableParent.WindowIsFocused = false;

            targetWindow.WindowIsFocused = true;

            if (isResizing)
            {
                PerformResize(targetWindow, resizingEdge, e.GetPosition(targetWindow));
            }
            else if (isDragging)
            {
                // Todo: fix window going to (0,0) every second time you click on it;
                var pos = e.GetPosition(this);
                var left = pos.X - startDragPosition.X;
                var top = pos.Y - startDragPosition.Y;
                ResizableWindow window = targetWindow;
                MoveWindow(window, left, top);
            }
        }

        private void MoveWindow(ResizableWindow window, double left, double top)
        {
            SetLeft(window, Math.Clamp(left, -resizeMargin, MaxWidth));
            SetTop(window, Math.Clamp(top, -resizeMargin, MaxHeight));
        }

        private void PerformResize(ResizableWindow window, ResizeEdge edge, Point relPos)
        {
            switch (edge)
            {
                case ResizeEdge.None:
                    break;
                case ResizeEdge.TopLeft:
                    relPos.X -= resizeMargin;
                    relPos.Y -= resizeMargin;
                    window.Resize(window.Width - relPos.X, window.Height - relPos.Y);
                    MoveWindow(window, GetLeft(window) + relPos.X, GetTop(window) + relPos.Y);
                    break;
                case ResizeEdge.TopCenter:
                    relPos.Y -= resizeMargin;
                    window.Resize(window.Width, window.Height - relPos.Y);
                    MoveWindow(window, GetLeft(window), GetTop(window) + relPos.Y);
                    break;
                case ResizeEdge.TopRight:
                    relPos.Y -= resizeMargin;
                    window.Resize(relPos.X + resizeMargin, window.Height - relPos.Y);
                    MoveWindow(window, GetLeft(window), GetTop(window) + relPos.Y);
                    break;
                case ResizeEdge.CenterLeft:
                    relPos.X -= resizeMargin;
                    window.Resize(window.Width - relPos.X, window.Height);
                    MoveWindow(window, GetLeft(window) + relPos.X, GetTop(window));
                    break;
                case ResizeEdge.CenterRight:
                    window.Resize(relPos.X + resizeMargin, window.Height);
                    break;
                case ResizeEdge.BottomLeft:
                    relPos.X -= resizeMargin;
                    relPos.Y += resizeMargin;
                    window.Resize(window.Width - relPos.X, relPos.Y);
                    MoveWindow(window, GetLeft(window) + relPos.X, GetTop(window));
                    break;
                case ResizeEdge.BottomCenter:
                    window.Resize(window.Width, relPos.Y + resizeMargin);
                    break;
                case ResizeEdge.BottomRight:
                    window.Resize(relPos.X + resizeMargin, relPos.Y + resizeMargin);
                    break;
                default:
                    break;
            }
        }

        internal void BeginMove(ResizableWindow window, Point mousePos)
        {
            if (isDragging)
                return;

            window.BringIntoViewAndToTop();
            startDragPosition = mousePos;
            targetWindow = window;
            isDragging = true;
        }

        internal void BeginResize(ResizableWindow window, ResizeEdge edge, Point relPos)
        {
            if (isResizing)
                return;
            window.BringIntoViewAndToTop();
            PerformResize(window, edge, relPos);
            resizingEdge = edge;
            targetWindow = window;
            isResizing = true;
        }
    }
}
