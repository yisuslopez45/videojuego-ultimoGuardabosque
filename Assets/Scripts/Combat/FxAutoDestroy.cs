using UnityEngine;

//Efecto de un solo uso (fogonazo, impacto, explosion): apaga su luz rapidamente y se destruye solo
public class FxAutoDestroy : MonoBehaviour
{
    public float lifetime = 1.5f;
    public float lightFadeTime = 0.08f;

    private Light fxLight;
    private float startIntensity;
    private float age;

    void Awake()
    {
        fxLight = GetComponentInChildren<Light>();
        if (fxLight != null) startIntensity = fxLight.intensity;
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        age += Time.deltaTime;
        if (fxLight != null) fxLight.intensity = Mathf.Lerp(startIntensity, 0f, age / lightFadeTime);
    }
}
