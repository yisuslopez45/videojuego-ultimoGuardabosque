using UnityEngine;
using UnityEngine.UIElements;

//Dibuja el anillo de la rueda de armas: un sector por opcion, el seleccionado resaltado
//con un arco dorado por fuera y el arma que se lleva en la mano marcada por dentro.
//Los iconos y textos son elementos normales que WeaponWheelUI coloca encima.
public class WeaponWheelRing : VisualElement
{
    public int SectorCount = 6;
    public int Selected = -1;
    public int Equipped = -1;
    public float OuterRadius = 300f;
    public float InnerRadius = 150f;
    [Tooltip("Separacion en grados entre sectores")]
    public float GapDegrees = 1.6f;

    //Paleta de bosque del juego (la misma que el inventario)
    public Color SectorColor = new Color32(28, 38, 30, 225);
    public Color SelectedColor = new Color32(70, 92, 70, 235);
    public Color CenterColor = new Color32(14, 20, 15, 235);
    public Color BorderColor = new Color32(122, 84, 52, 255);
    public Color AccentColor = new Color32(222, 180, 82, 255);

    public WeaponWheelRing()
    {
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
    }

    //Angulo del centro del sector i medido desde arriba en sentido horario (0 = arriba)
    public static float SectorAngleFromTop(int index, int count)
    {
        return index * 360f / count;
    }

    void Draw(MeshGenerationContext ctx)
    {
        if (SectorCount <= 0) return;
        var p = ctx.painter2D;
        Vector2 c = contentRect.center;
        float step = 360f / SectorCount;

        //Disco central (detras del texto de informacion)
        p.fillColor = CenterColor;
        Circle(p, c, InnerRadius - 8f);
        p.Fill();

        for (int i = 0; i < SectorCount; i++)
        {
            //En UI Toolkit los angulos empiezan en +X y crecen en sentido horario: arriba es -90
            float mid = -90f + SectorAngleFromTop(i, SectorCount);
            float a0 = mid - step * 0.5f + GapDegrees * 0.5f;
            float a1 = mid + step * 0.5f - GapDegrees * 0.5f;

            p.fillColor = i == Selected ? SelectedColor : SectorColor;
            p.BeginPath();
            p.Arc(c, OuterRadius, Angle.Degrees(a0), Angle.Degrees(a1), ArcDirection.Clockwise);
            p.Arc(c, InnerRadius, Angle.Degrees(a1), Angle.Degrees(a0), ArcDirection.CounterClockwise);
            p.ClosePath();
            p.Fill();

            //Arma en la mano: linea dorada fina en el borde interior
            if (i == Equipped)
            {
                p.strokeColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.7f);
                p.lineWidth = 3f;
                p.lineCap = LineCap.Round;
                p.BeginPath();
                p.Arc(c, InnerRadius + 6f, Angle.Degrees(a0 + 4f), Angle.Degrees(a1 - 4f), ArcDirection.Clockwise);
                p.Stroke();
            }
        }

        //Borde exterior de madera
        p.strokeColor = BorderColor;
        p.lineWidth = 3f;
        Circle(p, c, OuterRadius + 3f);
        p.Stroke();

        //Seleccionado: arco dorado grueso por fuera del anillo
        if (Selected >= 0 && Selected < SectorCount)
        {
            float mid = -90f + SectorAngleFromTop(Selected, SectorCount);
            p.strokeColor = AccentColor;
            p.lineWidth = 8f;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.Arc(c, OuterRadius + 13f, Angle.Degrees(mid - step * 0.5f + 3f), Angle.Degrees(mid + step * 0.5f - 3f), ArcDirection.Clockwise);
            p.Stroke();
        }
    }

    static void Circle(Painter2D p, Vector2 center, float radius)
    {
        p.BeginPath();
        p.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(180f), ArcDirection.Clockwise);
        p.Arc(center, radius, Angle.Degrees(180f), Angle.Degrees(360f), ArcDirection.Clockwise);
        p.ClosePath();
    }
}
