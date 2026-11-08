using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LetSmall
{
    internal static class UiMotion
    {
        internal static void Initialize(Application app)
        {
            UpdateResources(app);
            SystemParameters.StaticPropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == "ClientAreaAnimation")
                    app.Dispatcher.Invoke(() =>
                    {
                        UpdateResources(app);
                        foreach (Window window in app.Windows) RefreshTransitions(window);
                    });
            };
        }

        private static void RefreshTransitions(DependencyObject element)
        {
            var frameworkElement = element as FrameworkElement;
            if (frameworkElement != null)
                foreach (VisualStateGroup group in VisualStateManager.GetVisualStateGroups(frameworkElement))
                    foreach (VisualTransition transition in group.Transitions)
                        transition.GeneratedDuration = new Duration(TimeSpan.FromMilliseconds(
                            SystemParameters.ClientAreaAnimation && transition.To != "Disabled" ? 120 : 0));
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
                RefreshTransitions(VisualTreeHelper.GetChild(element, index));
        }

        private static void UpdateResources(Application app)
        {
            app.Resources["InteractionDuration"] = new Duration(TimeSpan.FromMilliseconds(
                SystemParameters.ClientAreaAnimation ? 120 : 0));
            app.Resources["PopupMotion"] = SystemParameters.ClientAreaAnimation
                ? PopupAnimation.Fade : PopupAnimation.None;
        }

        public static readonly DependencyProperty RevealOnShowProperty = DependencyProperty.RegisterAttached(
            "RevealOnShow", typeof(bool), typeof(UiMotion), new PropertyMetadata(false, RevealOnShowChanged));

        public static bool GetRevealOnShow(DependencyObject element)
        {
            return (bool)element.GetValue(RevealOnShowProperty);
        }

        public static void SetRevealOnShow(DependencyObject element, bool value)
        {
            element.SetValue(RevealOnShowProperty, value);
        }

        private static void RevealOnShowChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;
            if ((bool)args.NewValue) element.IsVisibleChanged += OnVisibleChanged;
            else element.IsVisibleChanged -= OnVisibleChanged;
        }

        private static void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            if ((bool)args.NewValue) Reveal((FrameworkElement)sender);
        }

        internal static void Reveal(FrameworkElement element)
        {
            if (element == null || !SystemParameters.ClientAreaAnimation) return;
            var duration = TimeSpan.FromMilliseconds(160);
            element.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, duration) { FillBehavior = FillBehavior.Stop });
            // Only animate our own translation; never replace a caller's transform.
            if (element.RenderTransform != Transform.Identity) return;
            var translate = new TranslateTransform();
            element.RenderTransform = translate;
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(5, 0, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            });
        }
    }
}
