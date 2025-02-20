using UnityEngine;

public class explosion : MonoBehaviour
{
    [SerializeField] int iDamage; // stores damage dealt by explosion

    [Header("Audio")]
    [Header("--------------------------")]
    public AudioSource aud;     // explosion audio source

    // explosion audio clip and clip volume
    [SerializeField] AudioClip[] aExplosionSound;
    [Range(0.0f, 1.0f)][SerializeField] float aExplosionSoundVol;

    void Start()
    {
        // when explosion is activated, play explosion audio clip
        aud.PlayOneShot(aExplosionSound[Random.Range(0, aExplosionSound.Length)], aExplosionSoundVol);
    }

    public void OnTriggerEnter(Collider other)
    {
        // if player or enemy is caught in explosion range
        if ((other.CompareTag("Player") || other.CompareTag("Enemy") || other.CompareTag("Boss")) && other.tag != tag)
        {
            if (CompareTag("Enemy") && other.CompareTag("Boss"))
            {
                return;
            }
            
            // apply physics pushback to player character
            GameManager._instance._playerScript.vPushBack =
                (GameManager._instance._player.transform.position - transform.position) * iDamage;


            Ray ray = new Ray(transform.position, other.transform.position - transform.position);
            RaycastHit hit;

            // Cast a ray if teh ray his a brick wall nothing else happens

            if (Physics.Raycast(ray, out hit, GetComponent<SphereCollider>().radius))
            {
                if (hit.collider.CompareTag("wall") || other.GetComponent<IDamageable>() == null)
                {
                    return;
                }
  
                // get target
                IDamageable isDamageable = other.GetComponent<IDamageable>();

                // apply damage
                isDamageable.TakeDamage(iDamage);
            }
        }
    }
}
