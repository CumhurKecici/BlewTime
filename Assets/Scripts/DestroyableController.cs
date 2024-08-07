using UnityEngine;

public class DestroyableController : MonoBehaviour, IDestroyable
{
    //Blow method for external call when explosion comes into contact with destroyable
    public void Blow()
    {
        //Destroys game object
        Destroy(gameObject);
        //Request powerup generation if possible
        MapBuilder.Instance.CreateEnviromentObject(EnviromentType.PowerUp, transform.position);
    }

}
