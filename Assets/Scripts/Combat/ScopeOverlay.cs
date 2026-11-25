using UnityEngine;
using UnityEngine.UIElements;

//Mira de francotirador a pantalla completa: todo negro salvo el circulo del catalejo,
//aro de laton y reticula (lineas finas en el centro, postes gruesos hacia el borde y puntos de distancia).
//La crea HudUI y la muestra mientras se mira por el catalejo.
public class ScopeOverlay : VisualElement
{
    [Tooltip("Radio del circulo respecto al lado mas corto de la pantalla")]
    public float RadiusFactor = 0.46f;

    //Paleta del juego: laton y madera del catalejo
    public Color OutsideColor = new Color32(6, 8, 6, 255);
    public Color BrassColor = new Color32(222, 180, 82, 255);
    public Color BrassDarkColor = new Color32(122, 84, 52, 255);
    public Color ReticleColor = new Color32(12, 12, 10, 235);

    public float Radius => Mathf.Min(contentRect.width, contentRect.height) * RadiusFactor;

    public ScopeOverlay()
    {
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
        //Se redibuja al cambiar el tamaño de la pantalla
        RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
    }

    void Draw(MeshGenerationContext ctx)
    {
        Rect r = contentRect;
        if (r.width <= 0f || r.height <= 0f) return;
        var p = ctx.painter2D;
        Vector2 c = r.center;
        float R = Radius;

        //1. Negro alrededor del circulo (rectangulo con un agujero redondo)
        p.fillColor = OutsideColor;
        p.BeginPath();
        p.MoveTo(new Vector2(r.xMin - 2f, r.yMin - 2f));
        p.LineTo(new Vector2(r.xMax + 2f, r.yMin - 2f));
        p.LineTo(new Vector2(r.xMax + 2f, r.yMax + 2f));
        p.LineTo(new Vector2(r.xMin - 2f, r.yMax + 2f));
        p.ClosePath();
        AddCircle(p, c, R);
        p.Fill(FillRule.OddEven);

        //2. Oscurecimiento del borde de la lente
        for (int i = 0; i < 8; i++)
        {
            p.strokeColor = new Color(0f, 0f, 0f, 0.42f * (1f - i / 8f));
            p.lineWidth = 7f;
            p.BeginPath();
            AddCircle(p, c, R - 3.5f - i * 6f);
            p.Stroke();
        }

        //3. Reticula
        p.strokeColor = ReticleColor;
        p.lineCap = LineCap.Butt;

        //Lineas finas que cruzan el centro
        p.lineWidth = 1.6f;
        Line(p, c + new Vector2(-R, 0f), c + new Vector2(R, 0f));
        Line(p, c + new Vector2(0f, -R), c + new Vector2(0f, R));

        //Postes gruesos desde el borde (izquierda, derecha y abajo), como en un catalejo de caza
        float postStart = R * 0.42f;
        p.lineWidth = 6f;
        Line(p, c + new Vector2(-R, 0f), c + new Vector2(-postStart, 0f));
        Line(p, c + new Vector2(R, 0f), c + new Vector2(postStart, 0f));
        Line(p, c + new Vector2(0f, R), c + new Vector2(0f, postStart));

        //Puntos de distancia en las lineas finas y marcas de caida debajo del centro
        p.fillColor = ReticleColor;
        float spacing = R * 0.085f;
        for (int i = 1; i <= 4; i++)
        {
            float d = spacing * i;
            Dot(p, c + new Vector2(d, 0f), 2.6f);
            Dot(p, c + new Vector2(-d, 0f), 2.6f);
            Dot(p, c + new Vector2(0f, -d), 2.6f);
            //Marcas horizontales de caida de la bala (mas cortas cuanto mas lejos)
            float half = spacing * (0.55f - i * 0.08f);
            p.lineWidth = 1.6f;
            Line(p, c + new Vector2(-half, d), c + new Vector2(half, d));
        }

        //Punto central muy pequeño
        Dot(p, c, 1.8f);

        //4. Aro del catalejo: madera oscura y filo de laton con marcas grabadas
        p.strokeColor = BrassDarkColor;
        p.lineWidth = 16f;
        p.BeginPath();
        AddCircle(p, c, R + 8f);
        p.Stroke();

        p.strokeColor = BrassColor;
        p.lineWidth = 3f;
        p.BeginPath();
        AddCircle(p, c, R + 1.5f);
        p.Stroke();

        p.strokeColor = new Color(BrassColor.r, BrassColor.g, BrassColor.b, 0.55f);
        p.lineWidth = 1.5f;
        p.BeginPath();
        AddCircle(p, c, R + 15f);
        p.Stroke();

        p.strokeColor = new Color(BrassColor.r, BrassColor.g, BrassColor.b, 0.7f);
        p.lineWidth = 2f;
        for (int i = 0; i < 48; i++)
        {
            float a = i * Mathf.PI * 2f / 48f;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float len = i % 4 == 0 ? 9f : 5f;
            Line(p, c + dir * (R + 4f), c + dir * (R + 4f + len));
        }
    }

    static void AddCircle(Painter2D p, Vector2 center, float radius)
    {
        p.MoveTo(center + new Vector2(radius, 0f));
        p.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(180f), ArcDirection.Clockwise);
        p.Arc(center, radius, Angle.Degrees(180f), Angle.Degrees(360f), ArcDirection.Clockwise);
        p.ClosePath();
    }

    static void Line(Painter2D p, Vector2 a, Vector2 b)
    {
        p.BeginPath();
        p.MoveTo(a);
        p.LineTo(b);
        p.Stroke();
    }

    static void Dot(Painter2D p, Vector2 center, float radius)
    {
        p.BeginPath();
        AddCircle(p, center, radius);
        p.Fill();
    }
}
