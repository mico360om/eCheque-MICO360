using System.Windows;

namespace eCheque.MICO360.Helpers
{
    /// <summary>
    /// Attached properties for sidebar nav buttons: a hover-proof "active" (selected) state and an
    /// optional notification badge (e.g. PDC-due count, update-available dot).
    /// </summary>
    public static class NavProps
    {
        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.RegisterAttached("IsActive", typeof(bool), typeof(NavProps), new PropertyMetadata(false));
        public static void SetIsActive(DependencyObject o, bool v) => o.SetValue(IsActiveProperty, v);
        public static bool GetIsActive(DependencyObject o) => (bool)o.GetValue(IsActiveProperty);

        public static readonly DependencyProperty BadgeProperty =
            DependencyProperty.RegisterAttached("Badge", typeof(string), typeof(NavProps), new PropertyMetadata(""));
        public static void SetBadge(DependencyObject o, string v) => o.SetValue(BadgeProperty, v ?? "");
        public static string GetBadge(DependencyObject o) => (string)o.GetValue(BadgeProperty);
    }
}
