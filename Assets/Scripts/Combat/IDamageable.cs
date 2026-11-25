using UnityEngine;

//Cualquier objeto que pueda recibir disparos (dianas, animales, enemigos...) implementa esta interfaz
public interface IDamageable
{
    void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection);
}
