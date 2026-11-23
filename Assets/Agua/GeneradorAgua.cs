using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Genera una malla de agua que rellena el lago y sigue los cauces del terreno.
// Se ejecuta en el editor (clic derecho en el componente > "Generar agua"); la malla se guarda como asset,
// así que en juego no hay ningún coste extra aparte de dibujarla.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GeneradorAgua : MonoBehaviour
{
    // Zona con su propio nivel de agua (plano) que varía a lo largo de Z, p. ej. un canal más alto que el lago.
    [System.Serializable]
    public class ZonaNivel
    {
        public string nombre = "Canal";
        public Rect zona;
        [Tooltip("Nivel del agua según la coordenada Z del mundo.")]
        public AnimationCurve nivelPorZ = AnimationCurve.Linear(0f, 1.3f, 200f, 1.3f);
    }

    [Header("Zona")]
    public Terrain terreno;
    [Tooltip("Punto dentro del lago desde el que se expande el agua.")]
    public Vector3 puntoSemilla = new Vector3(115f, 0f, 50f);
    public Vector2 zonaMin = new Vector2(0f, 0f);
    public Vector2 zonaMax = new Vector2(200f, 200f);
    [Tooltip("Rectángulos (x, z, ancho, largo) donde nunca se pone agua.")]
    public List<Rect> zonasExcluidas = new List<Rect> { new Rect(0f, 91f, 75f, 109f) };
    [Min(0.25f)] public float tamCelda = 1f;

    [Header("Lago")]
    public float nivelLago = 1.3f;
    [Tooltip("Zonas (como el canal norte) con un nivel de agua propio, más alto que el lago.")]
    public List<ZonaNivel> zonasNivel = new List<ZonaNivel>();
    [Tooltip("Cuánto se mete la superficie plana dentro de la orilla para que no se vea el borde.")]
    public float margenOrilla = 1.2f;

    [Header("Textura guía")]
    [Tooltip("Usa las zonas pintadas de azul en la textura del terreno (capa 0) como máscara del agua.")]
    public bool usarTexturaAzul = true;
    [Tooltip("Zona donde 'Excavar terreno' puede bajar el terreno para que el lago llene lo pintado de azul.")]
    public Rect zonaExcavar = new Rect(75f, 0f, 90f, 75f);
    public float profundidadExcavar = 0.9f;

    [Tooltip("Distancia máxima (m) a lo pintado de azul a la que puede haber agua de arroyo.")]
    public float distanciaMaxAzul = 2f;

    [Header("Arroyos")]
    [Tooltip("Qué tan hundido debe estar un punto respecto a su entorno para considerarse cauce.")]
    public float umbralCauce = 0.6f;
    public float radioCauce = 5f;
    public float alturaMaxCauce = 14f;
    [Tooltip("Distancia a la que un punto bajo el nivel del lago convierte la celda en orilla plana.")]
    public float radioOrillaLago = 4f;
    [Tooltip("Lámina de agua sobre el fondo del cauce.")]
    public float alturaSobreCauce = 0.6f;
    [Tooltip("Radio para buscar el fondo del cauce.")]
    public float radioFondo = 3f;
    [Range(0, 12)] public int pasadasSuavizado = 5;
    [Tooltip("Lámina mínima de agua sobre el fondo tras suavizar (evita que asomen los bordes de los saltos).")]
    public float laminaMinima = 0.90f;
    [Tooltip("Cuántas celdas se extiende el agua por el piso del cañón hasta las paredes.")]
    [Range(0, 10)] public int pasadasRellenoPiso = 5;
    [Tooltip("Pendiente del agua (grados) a partir de la cual aparece espuma de cascada.")]
    public float pendienteCascada = 22f;

    bool[] mascaraAzul;
    int anchoMascara, altoMascara;

    [ContextMenu("Generar agua")]
    public void Generar()
    {
        if (terreno == null) terreno = Terrain.activeTerrain;
        TerrainData td = terreno.terrainData;
        Vector3 to = terreno.transform.position;

        CargarMascara(td);

        int nx = Mathf.FloorToInt((zonaMax.x - zonaMin.x) / tamCelda) + 1;
        int nz = Mathf.FloorToInt((zonaMax.y - zonaMin.y) / tamCelda) + 1;
        var alt = new float[nx, nz];
        var cauce = new bool[nx, nz];
        var candidato = new bool[nx, nz];
        var cercaAzul = new bool[nx, nz];

        for (int x = 0; x < nx; x++)
        for (int z = 0; z < nz; z++)
        {
            Vector2 p = Punto(x, z);
            float h = Altura(td, to, p);
            alt[x, z] = h;
            float media = 0f;
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f;
                media += Altura(td, to, p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radioCauce);
            }
            media /= 8f;
            bool azul = Azul(td, to, p, tamCelda * 1.5f);
            // Si cerca hay terreno bajo el nivel del lago, es orilla del lago (agua plana), no un arroyo.
            // Un cauce hundido siempre cuenta (así la desembocadura llega al lago).
            float nivel = NivelEn(p);
            bool enZona = nivel > nivelLago + 1e-3f;
            cauce[x, z] = !enZona && h >= nivelLago && h < alturaMaxCauce
                          && (media - h > umbralCauce || (azul && MinimoCercano(td, to, p, radioOrillaLago) >= nivelLago));
            // Con textura guía, los arroyos no se salen de lo pintado de azul (evita charcos sueltos en las repisas).
            cercaAzul[x, z] = mascaraAzul == null || Azul(td, to, p, distanciaMaxAzul);
            if (!cercaAzul[x, z]) cauce[x, z] = false;
            candidato[x, z] = (h < nivel + margenOrilla || cauce[x, z]) && !Excluido(p);
        }

        // Relleno por inundación desde la semilla para quedarnos solo con el agua conectada al lago.
        var dentro = new bool[nx, nz];
        int sx = Mathf.Clamp(Mathf.RoundToInt((puntoSemilla.x - zonaMin.x) / tamCelda), 0, nx - 1);
        int sz = Mathf.Clamp(Mathf.RoundToInt((puntoSemilla.z - zonaMin.y) / tamCelda), 0, nz - 1);
        var cola = new Queue<Vector2Int>();
        if (candidato[sx, sz]) { dentro[sx, sz] = true; cola.Enqueue(new Vector2Int(sx, sz)); }
        var vecinos = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        while (cola.Count > 0)
        {
            Vector2Int c = cola.Dequeue();
            foreach (var d in vecinos)
            {
                Vector2Int v = c + d;
                if (v.x < 0 || v.y < 0 || v.x >= nx || v.y >= nz || dentro[v.x, v.y] || !candidato[v.x, v.y]) continue;
                // Desde un cauce solo se avanza a otro cauce o al agua del lago, no a la orilla.
                if (cauce[c.x, c.y] && !cauce[v.x, v.y] && alt[v.x, v.y] >= NivelEn(Punto(v.x, v.y))) continue;
                dentro[v.x, v.y] = true;
                cola.Enqueue(v);
            }
        }

        var indice = new int[nx, nz];
        var verts = new List<Vector3>();
        var colores = new List<Color>();
        var flujo = new List<Vector2>();
        // 1) Dirección del flujo: gradiente del terreno muy suavizado dentro del agua.
        var suave = (float[,])alt.Clone();
        for (int it = 0; it < 10; it++)
        {
            var copia = (float[,])suave.Clone();
            for (int x = 0; x < nx; x++)
            for (int z = 0; z < nz; z++)
            {
                if (!dentro[x, z]) continue;
                float suma = copia[x, z];
                int n = 1;
                foreach (var d in vecinos)
                {
                    int vx = x + d.x, vz = z + d.y;
                    if (vx < 0 || vz < 0 || vx >= nx || vz >= nz || !dentro[vx, vz]) continue;
                    suma += copia[vx, vz];
                    n++;
                }
                suave[x, z] = suma / n;
            }
        }

        // 2) Altura del agua: plana en el lago; en los arroyos = fondo del cauce (buscado de lado a lado,
        //    perpendicular al flujo) + lámina de agua. Así las orillas quedan por encima y se ve el cauce hundido,
        //    pero en las cascadas el agua sigue la caída.
        var yAgua = new float[nx, nz];
        var fondoCelda = new float[nx, nz];
        var flujoCelda = new Vector2[nx, nz];
        for (int x = 0; x < nx; x++)
        for (int z = 0; z < nz; z++)
        {
            if (!dentro[x, z]) continue;
            if (!cauce[x, z]) { yAgua[x, z] = NivelEn(Punto(x, z)); continue; }
            float gx = YVecino(suave, dentro, x + 1, z, suave[x, z]) - YVecino(suave, dentro, x - 1, z, suave[x, z]);
            float gz = YVecino(suave, dentro, x, z + 1, suave[x, z]) - YVecino(suave, dentro, x, z - 1, suave[x, z]);
            Vector2 abajo = new Vector2(-gx, -gz);
            abajo = abajo.sqrMagnitude > 1e-6f ? abajo.normalized : Vector2.right;
            flujoCelda[x, z] = abajo;
            Vector2 lado = new Vector2(-abajo.y, abajo.x);
            Vector2 p = Punto(x, z);
            float fondo = alt[x, z];
            for (float t = 0.5f; t <= radioFondo; t += 0.5f)
            {
                fondo = Mathf.Min(fondo, Altura(td, to, p + lado * t));
                fondo = Mathf.Min(fondo, Altura(td, to, p - lado * t));
            }
            fondoCelda[x, z] = fondo;
            yAgua[x, z] = Mathf.Max(nivelLago, fondo + alturaSobreCauce);
        }

        // 2b) Rellenar el piso del cañón: celdas vecinas a un arroyo cuyo terreno queda por debajo del agua
        //     pasan a ser agua, hasta topar con las paredes.
        for (int it = 0; it < pasadasRellenoPiso; it++)
        {
            var nuevas = new List<Vector3Int>();
            var yNueva = new List<float>();
            for (int x = 0; x < nx; x++)
            for (int z = 0; z < nz; z++)
            {
                if (alt[x, z] < nivelLago || !cercaAzul[x, z] || Excluido(Punto(x, z))) continue;
                // Las celdas planas del lago que asoman sobre el agua junto a la desembocadura también se cubren
                // (solo las más cercanas al arroyo, para no volver a levantar la orilla del lago).
                bool orillaDesembocadura = dentro[x, z] && !cauce[x, z] && it < 2 && alt[x, z] > NivelEn(Punto(x, z)) + 0.05f;
                if (dentro[x, z] && !orillaDesembocadura) continue;
                float mejorY = float.MinValue;
                float fondoV = float.MaxValue;
                foreach (var d in vecinos)
                {
                    int vx = x + d.x, vz = z + d.y;
                    if (vx < 0 || vz < 0 || vx >= nx || vz >= nz || !dentro[vx, vz] || !cauce[vx, vz]) continue;
                    if (yAgua[vx, vz] > mejorY) { mejorY = yAgua[vx, vz]; fondoV = fondoCelda[vx, vz]; }
                }
                // Solo piso: terreno por debajo del agua y cerca del nivel del fondo (no repisas en lo alto del cañón).
                if (mejorY > float.MinValue && alt[x, z] < mejorY - 0.1f && alt[x, z] < fondoV + 1f)
                {
                    nuevas.Add(new Vector3Int(x, z, 0));
                    yNueva.Add(mejorY);
                }
            }
            if (nuevas.Count == 0) break;
            for (int i = 0; i < nuevas.Count; i++)
            {
                int x = nuevas[i].x, z = nuevas[i].y;
                dentro[x, z] = true;
                cauce[x, z] = true;
                yAgua[x, z] = yNueva[i];
                fondoCelda[x, z] = Mathf.Min(alt[x, z], yNueva[i] - alturaSobreCauce);
                // Dirección de flujo del vecino más parecido.
                foreach (var d in vecinos)
                {
                    int vx = x + d.x, vz = z + d.y;
                    if (vx >= 0 && vz >= 0 && vx < nx && vz < nz && flujoCelda[vx, vz] != Vector2.zero) { flujoCelda[x, z] = flujoCelda[vx, vz]; break; }
                }
            }
        }

        // 3) Suavizado a lo largo del arroyo para quitar escalones (el lago no se toca).
        for (int it = 0; it < pasadasSuavizado; it++)
        {
            var copia = (float[,])yAgua.Clone();
            for (int x = 0; x < nx; x++)
            for (int z = 0; z < nz; z++)
            {
                if (!dentro[x, z] || !cauce[x, z]) continue;
                float suma = copia[x, z];
                int n = 1;
                foreach (var d in vecinos)
                {
                    int vx = x + d.x, vz = z + d.y;
                    if (vx < 0 || vz < 0 || vx >= nx || vz >= nz || !dentro[vx, vz]) continue;
                    suma += copia[vx, vz];
                    n++;
                }
                // Nunca por debajo de una lámina mínima sobre el fondo: así el agua pasa por encima
                // del borde de cada salto y la cascada queda unida.
                float yy = Mathf.Max(suma / n, fondoCelda[x, z] + laminaMinima);
                // Las celdas del piso (no las orillas) siempre quedan cubiertas, aunque tengan un escalón.
                if (alt[x, z] - fondoCelda[x, z] < 0.8f) yy = Mathf.Max(yy, alt[x, z] + 0.2f);
                yAgua[x, z] = Mathf.Max(nivelLago, yy);
            }
        }

        // 4) Vértices. La espuma depende de la pendiente del propio agua (solo donde de verdad cae).
        for (int x = 0; x < nx; x++)
        for (int z = 0; z < nz; z++)
        {
            indice[x, z] = -1;
            if (!dentro[x, z]) continue;
            float h = alt[x, z];
            Vector2 p = Punto(x, z);
            bool borde = false;
            foreach (var d in vecinos)
            {
                int vx = x + d.x, vz = z + d.y;
                if (vx < 0 || vz < 0 || vx >= nx || vz >= nz || !dentro[vx, vz]) borde = true;
            }

            float y = yAgua[x, z];
            float ola = 1f;
            float rapidos = 0f;
            float profundo = 0f;
            Vector2 dir = Vector2.zero;
            if (cauce[x, z])
            {
                // Pendiente del agua a partir de las celdas vecinas.
                float gx = (YVecino(yAgua, dentro, x + 1, z, y) - YVecino(yAgua, dentro, x - 1, z, y)) / (2f * tamCelda);
                float gz = (YVecino(yAgua, dentro, x, z + 1, y) - YVecino(yAgua, dentro, x, z - 1, y)) / (2f * tamCelda);
                Vector2 grad = new Vector2(gx, gz);
                float pendiente = Mathf.Atan(grad.magnitude) * Mathf.Rad2Deg;
                rapidos = Mathf.Clamp01((pendiente - pendienteCascada) / 25f);
                profundo = 1f - rapidos;
                dir = flujoCelda[x, z] * Mathf.Lerp(0.5f, 1.6f, rapidos);
                ola = borde ? 0f : 0.35f;
                if (borde && y > h - 0.1f) y = h - 0.15f;
            }

            indice[x, z] = verts.Count;
            verts.Add(transform.InverseTransformPoint(new Vector3(p.x, y, p.y)));
            colores.Add(new Color(rapidos, profundo, 0f, ola));
            flujo.Add(dir);
        }

        var tris = new List<int>();
        for (int x = 0; x < nx - 1; x++)
        for (int z = 0; z < nz - 1; z++)
        {
            int a = indice[x, z], b = indice[x, z + 1], c = indice[x + 1, z + 1], d = indice[x + 1, z];
            // Alternar la diagonal da un patrón de triángulos más "low-poly".
            if (((x + z) & 1) == 0)
            {
                AddTri(tris, a, b, c);
                AddTri(tris, a, c, d);
                if (a < 0 || c < 0) { AddTri(tris, a, b, d); AddTri(tris, b, c, d); }
            }
            else
            {
                AddTri(tris, a, b, d);
                AddTri(tris, b, c, d);
                if (b < 0 || d < 0) { AddTri(tris, a, b, c); AddTri(tris, a, c, d); }
            }
        }

        Mesh mesh = null;
#if UNITY_EDITOR
        const string ruta = "Assets/Agua/AguaLago.asset";
        // Se rellena el asset existente en lugar de copiarle datos: así la GPU recibe la malla nueva.
        mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
#endif
        bool nueva = mesh == null;
        if (nueva) mesh = new Mesh { name = "AguaLago" };
        mesh.Clear();
        mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetColors(colores);
        mesh.SetUVs(0, flujo);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        // Margen en los bounds para las olas del vertex shader.
        Bounds bnd = mesh.bounds;
        bnd.Expand(new Vector3(0f, 1f, 0f));
        mesh.bounds = bnd;

#if UNITY_EDITOR
        if (nueva) AssetDatabase.CreateAsset(mesh, ruta);
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(this);
#endif
        GetComponent<MeshFilter>().sharedMesh = mesh;
        Debug.Log($"Agua generada: {verts.Count} vértices, {tris.Count / 3} triángulos.");
    }

#if UNITY_EDITOR
    [ContextMenu("Excavar terreno según textura azul")]
    public void ExcavarTerreno()
    {
        if (terreno == null) terreno = Terrain.activeTerrain;
        TerrainData td = terreno.terrainData;
        Vector3 to = terreno.transform.position;
        CargarMascara(td);
        Undo.RegisterCompleteObjectUndo(td, "Excavar lago");

        int res = td.heightmapResolution;
        float paso = td.size.x / (res - 1);
        int x0 = Mathf.Clamp(Mathf.FloorToInt((zonaExcavar.xMin - to.x) / paso), 0, res - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((zonaExcavar.yMin - to.z) / paso), 0, res - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((zonaExcavar.xMax - to.x) / paso), 0, res - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((zonaExcavar.yMax - to.z) / paso), 0, res - 1);
        float[,] hs = td.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
        float objetivo = (nivelLago - profundidadExcavar - to.y) / td.size.y;
        int cambios = 0;
        for (int z = 0; z <= z1 - z0; z++)
        for (int x = 0; x <= x1 - x0; x++)
        {
            Vector2 p = new Vector2(to.x + (x0 + x) * paso, to.z + (z0 + z) * paso);
            if (Excluido(p)) continue;
            // Fracción de azul alrededor del punto: da una orilla suave que coincide con lo pintado.
            float w = FraccionAzul(td, to, p, 1.5f);
            if (w <= 0f) continue;
            w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(w / 0.6f));
            float nuevo = Mathf.Min(hs[z, x], Mathf.Lerp(hs[z, x], objetivo, w));
            if (nuevo < hs[z, x] - 1e-5f) { hs[z, x] = nuevo; cambios++; }
        }
        td.SetHeights(x0, z0, hs);
        EditorUtility.SetDirty(td);
        Debug.Log($"Terreno excavado: {cambios} puntos del heightmap bajados.");
    }
#endif

    void CargarMascara(TerrainData td)
    {
        mascaraAzul = null;
        if (!usarTexturaAzul || td.terrainLayers.Length == 0 || td.terrainLayers[0].diffuseTexture == null) return;
        Texture2D tex = td.terrainLayers[0].diffuseTexture;
        // La textura no es legible desde CPU: se copia a través de la GPU.
        var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(tex, rt);
        var previo = RenderTexture.active;
        RenderTexture.active = rt;
        var copia = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false, false);
        copia.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        copia.Apply();
        RenderTexture.active = previo;
        RenderTexture.ReleaseTemporary(rt);

        Color32[] px = copia.GetPixels32();
        DestroyImmediate(copia);
        anchoMascara = tex.width;
        altoMascara = tex.height;
        mascaraAzul = new bool[px.Length];
        for (int i = 0; i < px.Length; i++)
            mascaraAzul[i] = px[i].b > px[i].r + 25 && px[i].b > px[i].g + 10;
    }

    bool AzulEn(TerrainData td, Vector3 origen, Vector2 p)
    {
        if (mascaraAzul == null) return false;
        TerrainLayer capa = td.terrainLayers[0];
        float u = Mathf.Repeat((p.x - origen.x + capa.tileOffset.x) / capa.tileSize.x, 1f);
        float v = Mathf.Repeat((p.y - origen.z + capa.tileOffset.y) / capa.tileSize.y, 1f);
        int ix = Mathf.Min((int)(u * anchoMascara), anchoMascara - 1);
        int iz = Mathf.Min((int)(v * altoMascara), altoMascara - 1);
        return mascaraAzul[iz * anchoMascara + ix];
    }

    // Azul en el punto o a 'radio' de distancia (dilata un poco la máscara para tapar las orillas).
    bool Azul(TerrainData td, Vector3 origen, Vector2 p, float radio)
    {
        if (AzulEn(td, origen, p)) return true;
        for (int k = 0; k < 8; k++)
        {
            float a = k * Mathf.PI / 4f;
            if (AzulEn(td, origen, p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radio)) return true;
        }
        return false;
    }

    float FraccionAzul(TerrainData td, Vector3 origen, Vector2 p, float radio)
    {
        int n = 0, azules = 0;
        for (float dz = -radio; dz <= radio + 1e-3f; dz += radio / 3f)
        for (float dx = -radio; dx <= radio + 1e-3f; dx += radio / 3f)
        {
            n++;
            if (AzulEn(td, origen, p + new Vector2(dx, dz))) azules++;
        }
        return (float)azules / n;
    }

    static float YVecino(float[,] y, bool[,] dentro, int x, int z, float porDefecto)
    {
        if (x < 0 || z < 0 || x >= y.GetLength(0) || z >= y.GetLength(1) || !dentro[x, z]) return porDefecto;
        return y[x, z];
    }

    static float MinimoCercano(TerrainData td, Vector3 origen, Vector2 p, float radio)
    {
        float m = Altura(td, origen, p);
        for (int k = 0; k < 8; k++)
        {
            float a = k * Mathf.PI / 4f;
            m = Mathf.Min(m, Altura(td, origen, p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radio));
        }
        return m;
    }

    float NivelEn(Vector2 p)
    {
        float nivel = nivelLago;
        foreach (var zn in zonasNivel)
            if (zn.zona.Contains(p)) nivel = Mathf.Max(nivel, zn.nivelPorZ.Evaluate(p.y));
        return nivel;
    }

    bool Excluido(Vector2 p)
    {
        foreach (var r in zonasExcluidas)
            if (r.Contains(p)) return true;
        return false;
    }

    Vector2 Punto(int x, int z) => new Vector2(zonaMin.x + x * tamCelda, zonaMin.y + z * tamCelda);

    static float Altura(TerrainData td, Vector3 origen, Vector2 p)
    {
        float u = Mathf.Clamp01((p.x - origen.x) / td.size.x);
        float v = Mathf.Clamp01((p.y - origen.z) / td.size.z);
        return td.GetInterpolatedHeight(u, v) + origen.y;
    }

    static void AddTri(List<int> tris, int a, int b, int c)
    {
        if (a < 0 || b < 0 || c < 0) return;
        tris.Add(a); tris.Add(b); tris.Add(c);
    }
}
