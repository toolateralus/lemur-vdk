﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Lemur.GUI {
    // TODO: we should use object pooling & a static class for notifications and handle their lifetime
    // in that class. this was nice & simple but it's very slow and does too much.

    /// <summary>
    /// UI Control used for displaying runtime notifications, often errors.
    /// </summary>
    public partial class NotificationControl : UserControl
    {
        const int NOTIFICATION_SIZE_X = 350, NOTIFICATION_SIZE_Y = 100;
        private DispatcherTimer fadeOutTimer;
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(string), typeof(NotificationControl), new PropertyMetadata(string.Empty));
        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }
        public void Start()
        {
            fadeOutTimer.Start();
        }
        public NotificationControl()
        {

            InitializeComponent();
            Loaded += OnLoaded;
            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;

            MaxHeight = NOTIFICATION_SIZE_Y;
            MaxWidth = NOTIFICATION_SIZE_X;

            HorizontalAlignment = HorizontalAlignment.Right;
            VerticalAlignment = VerticalAlignment.Bottom;

            DataContext = this;


            fadeOutTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };

            fadeOutTimer.Tick += OnFadeOutTimerTick;
            MouseDoubleClick += NotificationControl_MouseDoubleClick;

        }
        private void NotificationControl_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Stop();
        }
        internal void Stop()
        {
            var parent = Parent as Panel;

            if (parent?.Children.Contains(this) is bool b && b)
                parent?.Children.Remove(this);
        }
        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            var fadeInAnimation = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1));
            BeginAnimation(OpacityProperty, fadeInAnimation);

            var margin = new Thickness(Margin.Left, Margin.Top, Margin.Right, Margin.Bottom + 15);
            var popUpAnim = new ThicknessAnimation(margin, TimeSpan.FromSeconds(1));
            BeginAnimation(MarginProperty, popUpAnim);
        }
        private void OnMouseEnter(object? sender, System.Windows.Input.MouseEventArgs e)
        {
            fadeOutTimer.Stop();
        }
        private void OnMouseLeave(object? sender, System.Windows.Input.MouseEventArgs e)
        {
            fadeOutTimer.Start();
        }
        private void OnFadeOutTimerTick(object? sender, EventArgs e)
        {
            var fadeOutAnimation = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(1.5));
            fadeOutAnimation.Completed += (s, _) =>
            {
                Stop();
            };
            BeginAnimation(OpacityProperty, fadeOutAnimation);
            fadeOutTimer.Stop();
        }

    }
}
