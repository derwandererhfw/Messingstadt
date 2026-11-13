using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Ausschnitt-Test für die Kulisse: Auf einer großen Karte liegt das meiste außerhalb des Bilds. Was dort steht,
    /// braucht weder gezeichnet noch je Bild neu berechnet zu werden. --ohne=ausschnitt schaltet den Test zum
    /// Vergleich ab, dann gilt alles als sichtbar.
    /// </summary>
    public static class ViewCull
    {
        /// <summary>Der Teil der Welt, den die Kamera gerade zeigt, um margin Bildpunkte der Welt erweitert.</summary>
        public static Rect2 Seen(CanvasItem item, float margin)
        {
            if (AirTest.Off("ausschnitt"))
            {
                return new Rect2(-1e6f, -1e6f, 2e6f, 2e6f);
            }
            var canvas = item.GetCanvasTransform();
            var origin = canvas.AffineInverse() * Vector2.Zero;
            var size = item.GetViewportRect().Size / canvas.Scale;
            return new Rect2(origin, size).Grow(margin);
        }
    }
}
