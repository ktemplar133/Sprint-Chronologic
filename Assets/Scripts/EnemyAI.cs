using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour, IDamageable
{
    [Header("Components")]
    [SerializeField] private LayerMask layers;
    [SerializeField] NavMeshAgent nAgent;               // enemy nav mesh
    [SerializeField] protected Renderer rRend;                    // enemy renderer
    [SerializeField] Animator aAnim;                        // enemy animator
    [SerializeField] private GameObject miniMapIcon;

    [Header("------------------------------")]
    [Header("Enemy Attributes")]
    [SerializeField] protected int iHP;                           // enemy health
    [SerializeField] protected int iViewAngle;                    // enemy field of view
    [SerializeField] protected int iPlayerFaceSpeed;              // speed at which enemy rotates to face player while tracking them
    [SerializeField] protected int iRoamingRadius;                // radius the enemy pathfinding is allowed to roam in

    protected Color _currentColor;                                  //Color for normal state of enemy
    protected Color _currentDamageColor;                            //Color for damage state of enemy

    [Header("------------------------------")]
    [Header("Weapon Stats")]
    [SerializeField] protected float fShootRate;                  // Rate at which enemy can fire their weapon
    [SerializeField] protected GameObject gBullet;                // stores enemy bullet object (can be used to store various object that will be used like the bullet is - example, grenades)
    [SerializeField] GameObject gShootPosition;         // stores position at which bullets are instantiated (in the case of guns, should be at the muzzle, for grenades it should be in an empty hand)
    protected GameObject _currentBullet;

    [Header("------------------------------")]
    [Header("Drops")]
    [SerializeField] GameObject gHealthPack;            // slot for drops - healthpack (not currently implemented)
    [SerializeField] GameObject gAmmoBox;               // slot for drops - ammo pack (not currently implemented)

    [SerializedField] public List<GameObject> listPowerUpDrops = new List<GameObject>();  //Power up list to drop

    [Header("------------------------------")]
    [Header("Audio")]
    public AudioSource aud;                             // enemy audio source

    // enemy audio clips and clip volume
    [SerializeField] AudioClip[] aGunShot;
    [Range(0.0f, 1.0f)][SerializeField] float aGunShotVol;

    [SerializeField] bool bCanShoot = true;                              // value for whether enemy can currently fire their weapon
    bool bPlayerInRange;                                // value tracking whether the player is in range of enemyAI
    public bool isGrenadier;
    public bool isBoss;
    public bool isGunner;

    private bool isDead;

    Vector3 vStartingPos;                               // vector storing enemy starting position
    Vector3 vPlayerDirection;                           // vector storing the direction the player is in from the perspective of the enemy

    float fStoppingDistanceOrig;                        // float value for how close enemy can get to other enemies, player, and etc
    //[SerializedField] public GameObject gDefusion;      //game object for defusion grenades

    [HideInInspector] int iHPOriginal;

    // Called at Start
    protected virtual void Awake()
    {
        vStartingPos = transform.position;                  // stores starting position
        fStoppingDistanceOrig = nAgent.stoppingDistance;    // stores stopping distance

        iHPOriginal = iHP;
        _currentBullet = gBullet;

        //Sets color to white for normal, red for damage
        _currentColor = Color.white;
        _currentDamageColor = Color.red;

        // update UI to reflect enemies placed in scene
        GameManager._instance.updateEnemyCount();
    }

    // Called every frame
    protected virtual void Update()
    {
        if (nAgent.isActiveAndEnabled) // if navmesh is enabled
        {
            // pass information to animator on how fast enemy is moving

            aAnim.SetFloat("Speed", Mathf.Lerp(aAnim.GetFloat("Speed"), nAgent.velocity.normalized.magnitude, Time.deltaTime * 5));

            // gets player direction for tracking player
            Vector3 playerTransform = GameManager._instance._player.transform.position;
            Vector3 tempPlayerPos = new Vector3(playerTransform.x, 1.11f, playerTransform.z);

            vPlayerDirection = tempPlayerPos - transform.position;

            if (bPlayerInRange) // if player is in range
            {
                //agent lets enemies know where player is
                nAgent.SetDestination(GameManager._instance._player.transform.position);

                CanSeePlayer();
                facePlayer();
            }
            else if (nAgent.remainingDistance < 0.1f)
            {
                roam();
            }
        }
    }

    void roam()
    {
        nAgent.stoppingDistance = 0;

        // get vector inside sphere multiplied by enemy roamingRadius
        Vector3 randomDirection = Random.insideUnitSphere * iRoamingRadius;
        randomDirection += vStartingPos;

        // sample direction to make sure its within bounds
        NavMeshHit hit;
        NavMesh.SamplePosition(randomDirection, out hit, iRoamingRadius, 1);
        NavMeshPath path = new NavMeshPath();

        // create new path based on path sampled above
        nAgent.CalculatePath(hit.position, path);
        nAgent.SetPath(path);
    }

    void facePlayer()
    {
        // if enemy has reached it's destination
        if (nAgent.remainingDistance <= nAgent.stoppingDistance)
        {
            Vector3 tempPos = new Vector3(vPlayerDirection.x, 0, vPlayerDirection.z);

            Quaternion rotation = Quaternion.LookRotation(tempPos);
            transform.rotation = Quaternion.Lerp(transform.rotation, rotation, Time.deltaTime * iPlayerFaceSpeed);
        }
    }

    void CanSeePlayer()
    {
        // return angle between player and enemy
        float angle = Vector3.Angle(vPlayerDirection, gShootPosition.transform.forward);

        RaycastHit hit;

        // determine if something is inbetween enemy and player 
        if (Physics.Raycast(gShootPosition.transform.position, GameManager._instance._player.transform.position - gShootPosition.transform.position, out hit, Mathf.Infinity, layers))
        {
            if (hit.collider.CompareTag("Player") && bCanShoot && angle <= iViewAngle)
            {
                StartCoroutine(Shoot());
            }
        }
    }

    // Function for checking if player is in range of enemy
    protected virtual void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            bPlayerInRange = true;
            nAgent.stoppingDistance = fStoppingDistanceOrig;

            if (isBoss)
            {
                GameManager._instance.SetBossHealthBarActive(true);
            }

        }
    }

    // Function for checking if player is leaving enemy's attack range
    protected virtual void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            bPlayerInRange = false;
            nAgent.stoppingDistance = 0;

            if (isBoss)
            {
                GameManager._instance.SetBossHealthBarActive(false);
            }
        }
    }

    public virtual void TakeDamage(int iDamage)
    {
        if (isBoss && iDamage > 10)
        {
            iDamage = 4;
        }
        //when enemy takes damage it flashes a color
        iHP -= iDamage;
        bPlayerInRange = true;

        aAnim.SetTrigger("Damage");
        StartCoroutine(FlashColor());

        //if enemy dies then enemy object is destroyed
        if (iHP <= 0)
        {

            GameManager._instance.CheckEnemyKills();
            if (isBoss)
            {
                GameManager._instance.CallWinGame();
            }
            else
            {
                DropPowerUp(); //Calls drop power-up function
            }

            isDead = true;
            aAnim.SetBool("Dead", true);

            // disable colliders
            DeathState();
        }
    }

    public void DeathState()
    {
        nAgent.enabled = false;
        bCanShoot = false;

        foreach (Collider col in GetComponents<Collider>())
        {
            col.enabled = false;
        }

        miniMapIcon.SetActive(false);
    }

    public bool GetIsDead()
    {
        return isDead;
    }

    IEnumerator FlashColor()
    {
        //flash color when hit
        rRend.material.color = _currentDamageColor;

        yield return new WaitForSeconds(0.1f);

        //return back to original color
        rRend.material.color = _currentColor;
    }

    IEnumerator Shoot()
    {
        //enemy can shoot player
        bCanShoot = false;
        aAnim.SetTrigger("Shoot");
        aud.PlayOneShot(aGunShot[Random.Range(0, aGunShot.Length)], aGunShotVol);
        Instantiate(_currentBullet, gShootPosition.transform.position, _currentBullet.transform.rotation);
        //setting up defusor when bullet is grenade

        yield return new WaitForSeconds(fShootRate);
        bCanShoot = true;
    }

    private void DropPowerUp()
    {
        if (GameManager._instance._playerScript.isReadyForDrop) //Checks if the power-up drop flag is true
        {
            Instantiate(listPowerUpDrops[Random.Range(0, 3)], transform.position + new Vector3(0, 1f, 0), listPowerUpDrops[Random.Range(0, 3)].transform.rotation); //Drops random power-up
            GameManager._instance._playerScript.isReadyForDrop = false;  //Sets power-up drop flag back to false
        }
    }
}
